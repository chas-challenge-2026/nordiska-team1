#include "database.hpp"

#include <array>
#include <cstdio>
#include <cstdlib>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <regex>
#include <sstream>
#include <stdexcept>
#include <unistd.h>
#include <sys/wait.h>

namespace fs = std::filesystem;

namespace nordiska::benchmark {

namespace {

std::string trim(std::string s) {
    auto first = s.find_first_not_of(" \t\r\n");
    if (first == std::string::npos) return "";
    auto last = s.find_last_not_of(" \t\r\n");
    return s.substr(first, last - first + 1);
}

std::string shell_quote(const std::string& arg) {
    std::string q = "'";
    for (char c : arg) {
        if (c == '\'') q += "'\\''";
        else q += c;
    }
    q += "'";
    return q;
}

std::string run_cmd(const std::vector<std::string>& args) {
    std::string cmd;
    for (const auto& a : args) {
        if (!cmd.empty()) cmd += " ";
        cmd += shell_quote(a);
    }

    FILE* pipe = popen(cmd.c_str(), "r");
    if (!pipe) throw std::runtime_error("Failed to run command: " + cmd);

    std::string out;
    char buffer[4096];
    while (size_t bytes = fread(buffer, 1, sizeof(buffer), pipe)) {
        out.append(buffer, bytes);
    }
    int status = pclose(pipe);
    if (status != 0) {
        throw std::runtime_error("Command exited with code " + std::to_string(WEXITSTATUS(status)) + ": " + cmd);
    }
    return trim(out);
}

fs::path find_repo_root() {
    fs::path current = fs::read_symlink("/proc/self/exe").parent_path();
    while (!current.empty()) {
        if (fs::exists(current / "infra/docker-compose.yml")) return current;
        auto parent = current.parent_path();
        if (parent == current) break;
        current = parent;
    }
    current = fs::current_path();
    while (!current.empty()) {
        if (fs::exists(current / "infra/docker-compose.yml")) return current;
        auto parent = current.parent_path();
        if (parent == current) break;
        current = parent;
    }
    throw std::runtime_error("Cannot locate repository root from executable or working directory");
}

std::string dotnet_json_str(const std::string& expr) {
    return "replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(replace("
           "to_json(" + expr + ")::text, "
           "'&', E'\\\\u0026'), "
           "'''', E'\\\\u0027'), "
           "'+', E'\\\\u002B'), "
           "'<', E'\\\\u003C'), "
           "'>', E'\\\\u003E'), "
           "'å', E'\\\\u00E5'), "
           "'ä', E'\\\\u00E4'), "
           "'ö', E'\\\\u00F6'), "
           "'Å', E'\\\\u00C5'), "
           "'Ä', E'\\\\u00C4'), "
           "'Ö', E'\\\\u00D6'), "
           "'é', E'\\\\u00E9'), "
           "'ü', E'\\\\u00FC')";
}

} // namespace

std::string DatabaseClient::detect_active_database() {
    fs::path root = find_repo_root();
    fs::path env_file = root / "infra/.env";
    if (!fs::exists(env_file)) env_file = root / "infra/v2/.env";

    if (fs::exists(env_file)) {
        std::ifstream file(env_file);
        std::string line;
        std::regex re{R"(^[ \t]*NORDISKA_DATABASE[ \t]*=[ \t]*(.*)$)"};
        while (std::getline(file, line)) {
            std::smatch match;
            if (std::regex_match(line, match, re)) {
                std::string val = trim(match[1]);
                if (!val.empty()) return val;
            }
        }
    }
    return "nordiska_synthetic";
}

std::string DatabaseClient::find_db_container() const {
    try {
        std::string id = run_cmd({
            "docker", "ps",
            "--filter", "label=com.docker.compose.project=nordiska-v2",
            "--filter", "label=com.docker.compose.service=db",
            "--format", "{{.ID}}"
        });
        if (!id.empty()) return id;
    } catch (...) {}
    return "nordiska-v2-db-1";
}

DatabaseClient::DatabaseClient(std::string database) {
    repo_root_ = find_repo_root();
    if (database.empty()) {
        database_ = detect_active_database();
    } else {
        database_ = database;
    }
    container_ = find_db_container();
}

std::string DatabaseClient::sql(const std::string& query) const {
    return run_cmd({
        "docker", "exec", "-i", container_,
        "psql", "-U", "nordiska_bootstrap", "-d", database_,
        "-v", "ON_ERROR_STOP=1", "-A", "-t", "-c", query
    });
}

DbCounts DatabaseClient::get_counts() const {
    const std::string query =
        "SELECT "
        "(SELECT count(*) FROM banking.customers) || '|' || "
        "(SELECT count(*) FROM banking.savings_accounts) || '|' || "
        "(SELECT count(*) FROM banking.ledger_entries) || '|' || "
        "(SELECT count(*) FROM reporting.generated_documents);";

    std::string out = sql(query);
    std::istringstream ss(out);
    DbCounts counts;
    std::string part;
    if (std::getline(ss, part, '|')) counts.customers = std::stoll(part);
    if (std::getline(ss, part, '|')) counts.accounts = std::stoll(part);
    if (std::getline(ss, part, '|')) counts.transactions = std::stoll(part);
    if (std::getline(ss, part, '|')) counts.documents = std::stoll(part);
    return counts;
}

void DatabaseClient::clean_reporting_data(bool clean_storage) const {
    const std::string clean_sql =
        "TRUNCATE TABLE "
        "reporting.account_statement_jobs, "
        "reporting.account_statement_documents, "
        "reporting.account_statement_entries, "
        "reporting.account_statements, "
        "reporting.tax_report_jobs, "
        "reporting.tax_report_documents, "
        "reporting.tax_reports, "
        "reporting.generated_documents "
        "CASCADE;";
    sql(clean_sql);

    if (clean_storage) {
        try {
            run_cmd({
                "docker", "exec", "nordiska-v2-reporting-worker-1",
                "sh", "-c", "rm -rf /var/lib/nordiska/report-documents/*"
            });
        } catch (...) {}
    }
}

int64_t DatabaseClient::stage_tax_reports(int year, int64_t limit) const {
    std::string limit_clause = limit > 0 ? "LIMIT " + std::to_string(limit) : "";

    std::string query =
        "WITH primary_accounts AS (\n"
        "    SELECT DISTINCT ON (a.\"CustomerId\")\n"
        "        a.\"Id\" AS \"AccountId\",\n"
        "        a.\"CustomerId\",\n"
        "        c.\"Name\" AS \"CustomerName\",\n"
        "        a.\"AccountNumber\",\n"
        "        COALESCE(a.\"AccountName\", a.\"AccountType\") AS \"AccountName\",\n"
        "        a.\"CurrencyCode\" AS \"Currency\"\n"
        "    FROM banking.savings_accounts a\n"
        "    JOIN banking.customers c ON c.\"Id\" = a.\"CustomerId\"\n"
        "    ORDER BY a.\"CustomerId\", a.\"Id\"\n"
        "    " + limit_clause + "\n"
        "),\n"
        "calc AS (\n"
        "    SELECT\n"
        "        pa.\"CustomerId\",\n"
        "        pa.\"AccountId\",\n"
        "        " + std::to_string(year) + " AS \"TaxYear\",\n"
        "        COALESCE(SUM(ROUND(t.\"Amount\" * 100)) FILTER (WHERE t.\"Type\" = 'interest'), 0)::bigint AS \"TotalInterestMinor\",\n"
        "        COALESCE(SUM(ABS(ROUND(t.\"Amount\" * 100))) FILTER (WHERE t.\"Type\" = 'tax'), 0)::bigint AS \"TaxDeductedMinor\",\n"
        "        pa.\"Currency\",\n"
        "        pa.\"CustomerName\",\n"
        "        pa.\"AccountNumber\",\n"
        "        pa.\"AccountName\",\n"
        "        now() AS \"CreatedAt\"\n"
        "    FROM primary_accounts pa\n"
        "    LEFT JOIN banking.ledger_entries t ON t.\"AccountId\" = pa.\"AccountId\"\n"
        "        AND NOT t.\"IsPlanned\"\n"
        "        AND t.\"CreatedAt\" >= '" + std::to_string(year) + "-01-01 00:00:00+00'\n"
        "        AND t.\"CreatedAt\" < '" + std::to_string(year + 1) + "-01-01 00:00:00+00'\n"
        "    GROUP BY pa.\"CustomerId\", pa.\"AccountId\", pa.\"Currency\", pa.\"CustomerName\", pa.\"AccountNumber\", pa.\"AccountName\"\n"
        "),\n"
        "reports_to_insert AS (\n"
        "    SELECT\n"
        "        \"CustomerId\", \"AccountId\", \"TaxYear\", \"TotalInterestMinor\", \"TaxDeductedMinor\",\n"
        "        \"Currency\", \"CustomerName\", \"AccountNumber\", \"AccountName\", \"CreatedAt\",\n"
        "        '1.0'::text AS \"SchemaVersion\",\n"
        "        encode(sha256(\n"
        "            convert_to(\n"
        "                '{\"customerId\":' || \"CustomerId\" ||\n"
        "                ',\"accountId\":' || \"AccountId\" ||\n"
        "                ',\"taxYear\":' || \"TaxYear\" ||\n"
        "                ',\"totalInterestMinor\":' || \"TotalInterestMinor\" ||\n"
        "                ',\"taxDeductedMinor\":' || \"TaxDeductedMinor\" ||\n"
        "                ',\"currency\":' || to_json(\"Currency\") ||\n"
        "                ',\"customerName\":' || " + dotnet_json_str("\"CustomerName\"") + " ||\n"
        "                ',\"accountNumber\":' || to_json(\"AccountNumber\") ||\n"
        "                ',\"accountName\":' || " + dotnet_json_str("\"AccountName\"") + " ||\n"
        "                ',\"createdAt\":' || to_json(to_char(\"CreatedAt\" at time zone 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS.US\"0Z\"')) ||\n"
        "                ',\"schemaVersion\":\"1.0\"}',\n"
        "                'UTF8'\n"
        "            )\n"
        "        ), 'hex') AS \"PayloadHash\",\n"
        "        'NotReported'::varchar AS \"ReportingStatus\"\n"
        "    FROM calc\n"
        "),\n"
        "inserted_reports AS (\n"
        "    INSERT INTO reporting.tax_reports (\n"
        "        \"CustomerId\", \"AccountId\", \"TaxYear\", \"TotalInterestMinor\", \"TaxDeductedMinor\",\n"
        "        \"Currency\", \"CustomerName\", \"AccountNumber\", \"AccountName\", \"CreatedAt\",\n"
        "        \"SchemaVersion\", \"PayloadHash\", \"ReportingStatus\"\n"
        "    )\n"
        "    SELECT\n"
        "        \"CustomerId\", \"AccountId\", \"TaxYear\", \"TotalInterestMinor\", \"TaxDeductedMinor\",\n"
        "        \"Currency\", \"CustomerName\", \"AccountNumber\", \"AccountName\", \"CreatedAt\",\n"
        "        \"SchemaVersion\", \"PayloadHash\", \"ReportingStatus\"\n"
        "    FROM reports_to_insert\n"
        "    RETURNING \"Id\", \"CreatedAt\"\n"
        "),\n"
        "inserted_jobs AS (\n"
        "    INSERT INTO reporting.tax_report_jobs (\n"
        "        \"TaxReportId\", \"Status\", \"AttemptCount\", \"CreatedAt\", \"AvailableAt\"\n"
        "    )\n"
        "    SELECT\n"
        "        \"Id\", 'Pending', 0, \"CreatedAt\", \"CreatedAt\" + interval '1 day'\n"
        "    FROM inserted_reports\n"
        "    RETURNING \"Id\"\n"
        ")\n"
        "SELECT count(*) FROM inserted_jobs;";

    std::string res = sql(query);
    return res.empty() ? 0 : std::stoll(res);
}

int64_t DatabaseClient::stage_account_statements(int year, int64_t limit) const {
    std::string limit_clause = limit > 0 ? "LIMIT " + std::to_string(limit) : "";
    std::string period_start = std::to_string(year) + "-01-01";
    std::string period_end = std::to_string(year) + "-12-31";
    std::string period_end_exclusive = std::to_string(year + 1) + "-01-01";

    std::string query =
        "WITH accounts_meta AS (\n"
        "    SELECT\n"
        "        a.\"Id\" AS acct_id,\n"
        "        a.\"CustomerId\" AS cust_id,\n"
        "        c.\"Name\" AS cust_name,\n"
        "        a.\"AccountNumber\" AS acct_num,\n"
        "        COALESCE(a.\"AccountName\", a.\"AccountType\") AS acct_name,\n"
        "        a.\"CurrencyCode\" AS curr,\n"
        "        ROUND((a.\"Balance\" - COALESCE((SELECT SUM(e.\"Amount\") FROM banking.ledger_entries e WHERE e.\"AccountId\" = a.\"Id\" AND NOT e.\"IsPlanned\" AND e.\"CreatedAt\" >= '" + period_end_exclusive + "'), 0)) * 100)::bigint AS closing_minor,\n"
        "        ROUND(COALESCE((SELECT SUM(e.\"Amount\") FROM banking.ledger_entries e WHERE e.\"AccountId\" = a.\"Id\" AND NOT e.\"IsPlanned\" AND e.\"CreatedAt\" >= '" + period_start + "' AND e.\"CreatedAt\" < '" + period_end_exclusive + "'), 0) * 100)::bigint AS movement_minor,\n"
        "        now() AS snap_at\n"
        "    FROM banking.savings_accounts a\n"
        "    JOIN banking.customers c ON c.\"Id\" = a.\"CustomerId\"\n"
        "    ORDER BY a.\"Id\"\n"
        "    " + limit_clause + "\n"
        "),\n"
        "account_entries_raw AS (\n"
        "    SELECT\n"
        "        m.acct_id,\n"
        "        ROW_NUMBER() OVER (PARTITION BY e.\"AccountId\" ORDER BY e.\"CreatedAt\", e.\"Id\") AS seq,\n"
        "        e.\"Id\" AS entry_id,\n"
        "        e.\"CreatedAt\" AS dt,\n"
        "        e.\"Type\" AS tp,\n"
        "        COALESCE(NULLIF(e.\"Label\", ''), e.\"Type\") AS descr,\n"
        "        ROUND(e.\"Amount\" * 100)::bigint AS amt,\n"
        "        (m.closing_minor - m.movement_minor) + SUM(ROUND(e.\"Amount\" * 100)::bigint) OVER (PARTITION BY e.\"AccountId\" ORDER BY e.\"CreatedAt\", e.\"Id\") AS bal_after\n"
        "    FROM accounts_meta m\n"
        "    JOIN banking.ledger_entries e ON e.\"AccountId\" = m.acct_id\n"
        "        AND NOT e.\"IsPlanned\"\n"
        "        AND e.\"CreatedAt\" >= '" + period_start + "' AND e.\"CreatedAt\" < '" + period_end_exclusive + "'\n"
        "),\n"
        "account_entries_agg AS (\n"
        "    SELECT\n"
        "        acct_id,\n"
        "        COALESCE('[' || string_agg(\n"
        "            '{\"sequenceNumber\":' || seq ||\n"
        "            ',\"sourceLedgerEntryId\":' || entry_id ||\n"
        "            ',\"bookedAt\":' || to_json(to_char(dt at time zone 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS.US\"0Z\"')) ||\n"
        "            ',\"type\":' || to_json(tp) ||\n"
        "            ',\"description\":' || " + dotnet_json_str("descr") + " ||\n"
        "            ',\"amountMinor\":' || amt ||\n"
        "            ',\"balanceAfterMinor\":' || bal_after ||\n"
        "            '}',\n"
        "            ','\n"
        "            ORDER BY seq\n"
        "        ) || ']', '[]') AS entries_json\n"
        "    FROM account_entries_raw\n"
        "    GROUP BY acct_id\n"
        "),\n"
        "stmts_to_insert AS (\n"
        "    SELECT\n"
        "        m.cust_id,\n"
        "        m.acct_id,\n"
        "        '" + period_start + "'::date AS period_start,\n"
        "        '" + period_end + "'::date AS period_end,\n"
        "        m.cust_name,\n"
        "        m.acct_num,\n"
        "        m.acct_name,\n"
        "        m.curr,\n"
        "        (m.closing_minor - m.movement_minor) AS opening_minor,\n"
        "        m.closing_minor,\n"
        "        m.snap_at,\n"
        "        '1.0'::varchar AS schema_ver,\n"
        "        encode(sha256(\n"
        "            convert_to(\n"
        "                '{\"customerId\":' || m.cust_id ||\n"
        "                ',\"accountId\":' || m.acct_id ||\n"
        "                ',\"periodStart\":\"" + period_start + "\"' ||\n"
        "                ',\"periodEnd\":\"" + period_end + "\"' ||\n"
        "                ',\"customerName\":' || " + dotnet_json_str("m.cust_name") + " ||\n"
        "                ',\"accountNumber\":' || to_json(m.acct_num) ||\n"
        "                ',\"accountName\":' || " + dotnet_json_str("m.acct_name") + " ||\n"
        "                ',\"currency\":' || to_json(m.curr) ||\n"
        "                ',\"openingBalanceMinor\":' || (m.closing_minor - m.movement_minor) ||\n"
        "                ',\"closingBalanceMinor\":' || m.closing_minor ||\n"
        "                ',\"snapshotAt\":' || to_json(to_char(m.snap_at at time zone 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS.US\"0Z\"')) ||\n"
        "                ',\"schemaVersion\":\"1.0\"' ||\n"
        "                ',\"entries\":' || COALESCE(agg.entries_json, '[]') ||\n"
        "                '}',\n"
        "                'UTF8'\n"
        "            )\n"
        "        ), 'hex') AS payload_hash\n"
        "    FROM accounts_meta m\n"
        "    LEFT JOIN account_entries_agg agg ON agg.acct_id = m.acct_id\n"
        "),\n"
        "inserted_stmts AS (\n"
        "    INSERT INTO reporting.account_statements (\n"
        "        \"CustomerId\", \"AccountId\", \"PeriodStart\", \"PeriodEnd\",\n"
        "        \"CustomerName\", \"AccountNumber\", \"AccountName\", \"Currency\",\n"
        "        \"OpeningBalanceMinor\", \"ClosingBalanceMinor\", \"SnapshotAt\",\n"
        "        \"SchemaVersion\", \"PayloadHash\"\n"
        "    )\n"
        "    SELECT\n"
        "        cust_id, acct_id, period_start, period_end,\n"
        "        cust_name, acct_num, acct_name, curr,\n"
        "        opening_minor, closing_minor, snap_at,\n"
        "        schema_ver, payload_hash\n"
        "    FROM stmts_to_insert\n"
        "    RETURNING \"Id\", \"AccountId\", \"SnapshotAt\"\n"
        "),\n"
        "inserted_entries AS (\n"
        "    INSERT INTO reporting.account_statement_entries (\n"
        "        \"AccountStatementId\", \"SequenceNumber\", \"SourceLedgerEntryId\",\n"
        "        \"BookedAt\", \"Type\", \"Description\", \"AmountMinor\", \"BalanceAfterMinor\"\n"
        "    )\n"
        "    SELECT\n"
        "        s.\"Id\",\n"
        "        r.seq,\n"
        "        r.entry_id,\n"
        "        r.dt,\n"
        "        r.tp,\n"
        "        r.descr,\n"
        "        r.amt,\n"
        "        r.bal_after\n"
        "    FROM inserted_stmts s\n"
        "    JOIN account_entries_raw r ON r.acct_id = s.\"AccountId\"\n"
        "    RETURNING 1\n"
        "),\n"
        "inserted_jobs AS (\n"
        "    INSERT INTO reporting.account_statement_jobs (\n"
        "        \"AccountStatementId\", \"Status\", \"AttemptCount\", \"CreatedAt\", \"AvailableAt\"\n"
        "    )\n"
        "    SELECT\n"
        "        \"Id\", 'Pending', 0, \"SnapshotAt\", \"SnapshotAt\" + interval '1 day'\n"
        "    FROM inserted_stmts\n"
        "    RETURNING \"Id\"\n"
        ")\n"
        "SELECT count(*) FROM inserted_jobs;";

    std::string res = sql(query);
    return res.empty() ? 0 : std::stoll(res);
}

void DatabaseClient::release_all_jobs() const {
    const std::string query =
        "UPDATE reporting.tax_report_jobs SET \"AvailableAt\" = now() WHERE \"AvailableAt\" > now();\n"
        "UPDATE reporting.account_statement_jobs SET \"AvailableAt\" = now() WHERE \"AvailableAt\" > now();";
    sql(query);
}

JobProgress DatabaseClient::poll_job_progress() const {
    const std::string query =
        "SELECT "
        "(SELECT count(*) FROM reporting.tax_report_jobs WHERE \"Status\" = 'Pending') + "
        "(SELECT count(*) FROM reporting.account_statement_jobs WHERE \"Status\" = 'Pending') || '|' || "
        "(SELECT count(*) FROM reporting.tax_report_jobs WHERE \"Status\" = 'Processing') + "
        "(SELECT count(*) FROM reporting.account_statement_jobs WHERE \"Status\" = 'Processing') || '|' || "
        "(SELECT count(*) FROM reporting.tax_report_jobs WHERE \"Status\" = 'Completed') + "
        "(SELECT count(*) FROM reporting.account_statement_jobs WHERE \"Status\" = 'Completed') || '|' || "
        "(SELECT count(*) FROM reporting.tax_report_jobs WHERE \"Status\" = 'Failed') + "
        "(SELECT count(*) FROM reporting.account_statement_jobs WHERE \"Status\" = 'Failed');";

    std::string out = sql(query);
    std::istringstream ss(out);
    JobProgress p;
    std::string part;
    if (std::getline(ss, part, '|')) p.pending = std::stoll(part);
    if (std::getline(ss, part, '|')) p.processing = std::stoll(part);
    if (std::getline(ss, part, '|')) p.completed = std::stoll(part);
    if (std::getline(ss, part, '|')) p.failed = std::stoll(part);
    return p;
}

LatencyStats DatabaseClient::query_latency_stats() const {
    const std::string query =
        "WITH all_jobs AS (\n"
        "    SELECT \"StartedAt\", \"CompletedAt\"\n"
        "    FROM reporting.tax_report_jobs\n"
        "    WHERE \"Status\" = 'Completed' AND \"StartedAt\" IS NOT NULL AND \"CompletedAt\" IS NOT NULL\n"
        "    UNION ALL\n"
        "    SELECT \"StartedAt\", \"CompletedAt\"\n"
        "    FROM reporting.account_statement_jobs\n"
        "    WHERE \"Status\" = 'Completed' AND \"StartedAt\" IS NOT NULL AND \"CompletedAt\" IS NOT NULL\n"
        ")\n"
        "SELECT\n"
        "    COALESCE(percentile_cont(0.50) WITHIN GROUP (ORDER BY EXTRACT(EPOCH FROM (\"CompletedAt\" - \"StartedAt\")) * 1000), 0) || '|' ||\n"
        "    COALESCE(percentile_cont(0.95) WITHIN GROUP (ORDER BY EXTRACT(EPOCH FROM (\"CompletedAt\" - \"StartedAt\")) * 1000), 0) || '|' ||\n"
        "    COALESCE(percentile_cont(0.99) WITHIN GROUP (ORDER BY EXTRACT(EPOCH FROM (\"CompletedAt\" - \"StartedAt\")) * 1000), 0) || '|' ||\n"
        "    COALESCE(avg(EXTRACT(EPOCH FROM (\"CompletedAt\" - \"StartedAt\")) * 1000), 0) || '|' ||\n"
        "    COALESCE(EXTRACT(EPOCH FROM (MAX(\"CompletedAt\") - MIN(\"StartedAt\"))), 0)\n"
        "FROM all_jobs;";

    std::string out = sql(query);
    std::istringstream ss(out);
    LatencyStats stats;
    std::string part;
    if (std::getline(ss, part, '|')) stats.p50_ms = std::stod(part);
    if (std::getline(ss, part, '|')) stats.p95_ms = std::stod(part);
    if (std::getline(ss, part, '|')) stats.p99_ms = std::stod(part);
    if (std::getline(ss, part, '|')) stats.avg_ms = std::stod(part);
    if (std::getline(ss, part, '|')) stats.db_span_sec = std::stod(part);
    return stats;
}

int64_t DatabaseClient::count_generated_documents() const {
    std::string out = sql("SELECT count(*) FROM reporting.generated_documents;");
    return out.empty() ? 0 : std::stoll(out);
}

} // namespace nordiska::benchmark
