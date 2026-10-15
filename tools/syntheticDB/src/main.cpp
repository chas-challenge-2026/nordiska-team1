#include "generator.hpp"
#include "fixture_login.hpp"
#include "generation_progress.hpp"

#include <iostream>
#include <vector>
#include <string>
#include <string_view>
#include <chrono>
#include <thread>
#include <mutex>
#include <atomic>
#include <format>
#include <cstdio>
#include <cstdlib>
#include <cmath>
#include <memory>
#include <algorithm>
#include <stdexcept>
#include <filesystem>
#include <fstream>
#include <unistd.h>
#include <condition_variable>
#include <deque>
#include <array>
#include <sys/wait.h>
#include <cerrno>
#include <iterator>

using namespace nordiska::synthetic;

static inline std::string format_num(int64_t val) {
    std::string s = std::to_string(val);
    int n = static_cast<int>(s.length()) - 3;
    while (n > 0) {
        s.insert(n, ",");
        n -= 3;
    }
    return s;
}

static std::string shell_quote(std::string_view value) {
    std::string result = "'";
    for (char ch : value) {
        if (ch == '\'') result += "'\\''";
        else result += ch;
    }
    return result + "'";
}

// Successful runs use one docker exec and one psql session. Data commit markers
// acknowledge successful COMMITs; control markers delimit metadata and DDL.
// ON_ERROR_STOP closes the channel on SQL failure.
class PsqlSession {
    FILE* input_ = nullptr;
    int output_ = -1;
    pid_t child_ = -1;
    std::string errors_;
public:
    PsqlSession(const PsqlSession&) = delete;
    PsqlSession& operator=(const PsqlSession&) = delete;

    PsqlSession(const std::string& container, const std::string& database) {
        auto path = (std::filesystem::temp_directory_path() / "nordiska-ingest.XXXXXX").string();
        int errors = mkstemp(path.data());
        if (errors < 0) throw std::runtime_error("Cannot capture PostgreSQL errors");
        errors_ = path;
        int in[2], out[2];
        if (pipe(in)) { ::close(errors); unlink(path.c_str()); throw std::runtime_error("Cannot create PostgreSQL input pipe"); }
        if (pipe(out)) {
            ::close(in[0]); ::close(in[1]); ::close(errors); unlink(path.c_str());
            throw std::runtime_error("Cannot create PostgreSQL output pipe");
        }
        auto command = "docker exec -i " + shell_quote(container) + " psql -X -U nordiska_bootstrap -d " +
            shell_quote(database) + " -v ON_ERROR_STOP=1 -Atq";
        child_ = fork();
        if (child_ == 0) {
            dup2(in[0], STDIN_FILENO); dup2(out[1], STDOUT_FILENO); dup2(errors, STDERR_FILENO);
            ::close(in[0]); ::close(in[1]); ::close(out[0]); ::close(out[1]); ::close(errors);
            execl("/bin/sh", "sh", "-c", command.c_str(), nullptr);
            _exit(127);
        }
        ::close(in[0]); ::close(out[1]); ::close(errors);
        if (child_ < 0) { ::close(in[1]); ::close(out[0]); unlink(path.c_str()); throw std::runtime_error("Cannot start PostgreSQL session"); }
        input_ = fdopen(in[1], "w"); output_ = out[0];
        if (!input_) {
            ::close(in[1]);
            close();
            throw std::runtime_error("Cannot open PostgreSQL input");
        }
        setvbuf(input_, nullptr, _IOFBF, 256 * 1024);
    }
    void write(std::string_view text) {
        if (fwrite(text.data(), 1, text.size(), input_) != text.size())
            throw std::runtime_error("PostgreSQL ingestion pipe write failed");
    }
    void flush() {
        if (fflush(input_) != 0) throw std::runtime_error("PostgreSQL ingestion pipe flush failed");
    }
    void barrier(int64_t serial, std::vector<std::string>* rows = nullptr, bool data_commit = true) {
        const auto marker = std::string(data_commit ? "NORDISKA_COMMIT_" : "NORDISKA_CONTROL_") + std::to_string(serial);
        write("\\echo " + marker + "\n");
        if (fflush(input_) != 0) throw std::runtime_error("PostgreSQL ingestion pipe flush failed");
        std::string line;
        char buffer[1024];
        for (;;) {
            auto count = ::read(output_, buffer, sizeof(buffer));
            if (count < 0 && errno == EINTR) continue;
            if (count <= 0) throw std::runtime_error("PostgreSQL ingestion failed before commit acknowledgement");
            for (ssize_t i = 0; i < count; ++i) {
                if (buffer[i] == '\n') {
                    if (line == marker) return;
                    if (rows) rows->push_back(line);
                    line.clear();
                } else line += buffer[i];
            }
        }
    }
    void close() {
        if (input_) { fclose(input_); input_ = nullptr; }
        if (output_ >= 0) { ::close(output_); output_ = -1; }
        int status = 0;
        if (child_ > 0) { while (waitpid(child_, &status, 0) < 0 && errno == EINTR) {} child_ = -1; }
        if (!errors_.empty()) {
            std::ifstream errors(errors_);
            std::cerr << errors.rdbuf();
            // Inserting an empty streambuf can set failbit on cerr.
            std::cerr.clear();
            unlink(errors_.c_str()); errors_.clear();
        }
        if (status != 0) throw std::runtime_error("PostgreSQL session exited with an error");
    }
    ~PsqlSession() { try { close(); } catch (const std::exception& e) { std::cerr << e.what() << '\n'; } }
};

// Only non-unique, non-constraint indexes on the otherwise-empty ledger table
// are deferred. Definitions stay in client memory so a failed session can be
// repaired through a fresh connection. Constraints and primary keys stay live.
class DeferredLedgerIndexes {
    struct Index { std::string name, definition; };
    std::vector<Index> indexes_;
    bool pending_ = false;
    static std::string decode_hex(std::string_view value) {
        if (value.size() % 2) throw std::runtime_error("Invalid PostgreSQL index metadata");
        auto digit = [](char c) -> unsigned {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            throw std::runtime_error("Invalid PostgreSQL index metadata");
        };
        std::string result;
        result.reserve(value.size() / 2);
        for (size_t i = 0; i < value.size(); i += 2) result += char(digit(value[i]) * 16 + digit(value[i+1]));
        return result;
    }
public:
    size_t size() const { return indexes_.size(); }
    bool pending() const { return pending_; }
    void discover(PsqlSession& session, int64_t& serial) {
        session.write(R"sql(SELECT 'NORDISKA_INDEX|' || encode(convert_to(format('%I.%I', ns.nspname, idx.relname), 'UTF8'), 'hex') || '|' || encode(convert_to(pg_get_indexdef(i.indexrelid), 'UTF8'), 'hex')
FROM pg_index i JOIN pg_class idx ON idx.oid=i.indexrelid JOIN pg_namespace ns ON ns.oid=idx.relnamespace
WHERE i.indrelid='banking.ledger_entries'::regclass AND NOT i.indisunique AND NOT i.indisprimary AND NOT i.indisreplident AND i.indisvalid
AND NOT EXISTS (SELECT 1 FROM pg_constraint c WHERE c.conindid=i.indexrelid)
AND NOT i.indisclustered AND obj_description(i.indexrelid, 'pg_class') IS NULL
AND NOT EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid=i.indexrelid AND (a.attstattarget>=0 OR col_description(a.attrelid,a.attnum) IS NOT NULL))
AND NOT EXISTS (SELECT 1 FROM pg_depend d WHERE d.classid='pg_class'::regclass AND d.objid=i.indexrelid AND d.deptype='e')
AND NOT EXISTS (SELECT 1 FROM banking.ledger_entries) ORDER BY i.indexrelid;
)sql");
        std::vector<std::string> rows;
        session.barrier(++serial, &rows, false);
        constexpr std::string_view prefix = "NORDISKA_INDEX|";
        for (const auto& row : rows) {
            if (!row.starts_with(prefix)) continue;
            const auto divider = row.find('|', prefix.size());
            if (divider == std::string::npos) throw std::runtime_error("Invalid PostgreSQL index metadata");
            Index index{decode_hex(std::string_view(row).substr(prefix.size(), divider - prefix.size())), decode_hex(std::string_view(row).substr(divider + 1))};
            if (!index.definition.starts_with("CREATE INDEX ")) throw std::runtime_error("Unsupported deferred index definition");
            indexes_.push_back(std::move(index));
        }
    }
    void defer(PsqlSession& session, int64_t& serial) {
        if (indexes_.empty()) return;
        // Set before sending: a lost acknowledgement can follow a successful DDL commit.
        pending_ = true;
        session.write("BEGIN;\n");
        for (const auto& index : indexes_) session.write("DROP INDEX " + index.name + ";\n");
        session.write("COMMIT;\n");
        session.barrier(++serial, nullptr, false);
    }
    void restore(PsqlSession& session, int64_t& serial,
                 const std::function<void(size_t, size_t)>& before = {}) {
        if (!pending_) return;
        for (size_t i = 0; i < indexes_.size(); ++i) {
            if (before) before(i, indexes_.size());
            auto sql = indexes_[i].definition;
            // Retry safely if a previous acknowledgement was lost or an earlier
            // index succeeded before a later index failed.
            sql.replace(0, 13, "CREATE INDEX IF NOT EXISTS ");
            session.write(sql + ";\n");
            session.barrier(++serial, nullptr, false);
        }
        // A present but invalid index must never count as a successful repair.
        for (const auto& index : indexes_) {
            session.write("DO $verify$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_index WHERE indexrelid=to_regclass(" +
                sql_literal(index.name) + ") AND indisvalid AND indisready AND pg_get_indexdef(indexrelid)=" +
                sql_literal(index.definition) + ") THEN RAISE EXCEPTION 'Deferred index restoration incomplete'; END IF; END $verify$;\n");
        }
        session.barrier(++serial, nullptr, false);
        pending_ = false;
    }
private:
    static std::string sql_literal(std::string_view value) {
        std::string result = "'";
        for (char c : value) { result += c; if (c == '\'') result += c; }
        return result + "'";
    }
};

struct WorkBlock {
    std::array<std::string, 4> rows;
    int64_t customers = 0, accounts = 0, transactions = 0;
    void clear() { for (auto& row : rows) row.clear(); customers = accounts = transactions = 0; }
    size_t bytes() const { size_t n = 0; for (const auto& row : rows) n += row.size(); return n; }
};

// A fixed inventory of reusable blocks bounds queued work, including producers.
// Scheduling units are 16 customers; commits use independent row/byte budgets.
class GenerationPool {
    const GeneratorConfig& cfg;
    PersonalNumberSequence& append_identities;
    bool appending;
    int64_t role;
    std::atomic<int64_t>& generated;
    std::mutex mutex;
    std::condition_variable changed;
    std::deque<std::unique_ptr<WorkBlock>> free, ready;
    std::vector<std::jthread> threads;
    int64_t next = 0, limit;
    bool stopping = false;
    std::exception_ptr failure;
    const int days_in_year;
    std::array<int, 12> month_days{};
    const std::string created_str, opening_date, deposit_label, withdrawal_label;
    std::array<std::array<std::string, 31>, 12> deposit_dates, withdrawal_dates;
    std::array<std::string, 12> interest_dates, tax_dates;
    void generate(WorkBlock& block, int64_t offset, int64_t count, const std::vector<std::string>& identities) {
        auto& cust_buf = block.rows[0]; auto& role_buf = block.rows[1];
        auto& acct_buf = block.rows[2]; auto& ledger_buf = block.rows[3];
        for (int64_t i = 0; i < count; ++i) {
            const auto c = cfg.start_customer_id + offset + i;
            FastRng phone_rng(stable_seed(cfg.master_seed, c));

            const auto pnum = appending ? identities[i] :
                generate_personnummer(cfg.master_seed, c - cfg.start_customer_id);

            // A separate random stream varies names without changing the banking workload.
            FastRng names(stable_seed(cfg.master_seed, c) ^ 0x8f3d51c2a7b649e0ULL);
            static constexpr const char* first_names[]{"Anna", "Erik", "Maria", "Lars", "Sara", "Johan", "Emma", "Anders",
                "Linnea", "Mohammed", "Fatima", "Nils", "Sofia", "Ali", "Elin", "Oscar"};
            static constexpr const char* last_names[]{"Andersson", "Johansson", "Karlsson", "Nilsson", "Eriksson", "Larsson",
                "Olsson", "Persson", "Svensson", "Gustafsson", "Lindberg", "Berg", "Ahmed", "Lindstrom", "Hassan", "Nyberg"};
            std::string fname = first_names[names.randint(0, 15)];
            std::string lname = last_names[names.randint(0, 15)];
            std::string full_name = fname + " " + lname;
            std::string email = std::format("cust{}@nordiska-demo.se", c);
            std::string norm_email = std::format("CUST{}@NORDISKA-DEMO.SE", c);
            std::string phone = std::format("+4670{:07d}", phone_rng.randint(1000000, 9999999));

            std::format_to(std::back_inserter(cust_buf),
                "{}\t{}\t{}\t{}\t{}\t{}\t{}\t{}\t{}\t\t\tt\tf\tf\tf\t0\t{}\n",
                c, pnum, full_name, email, norm_email, email, norm_email, phone,
                fixture_password_hash,
                created_str
            );
            std::format_to(std::back_inserter(role_buf), "{}\t{}\n", c, role);

            FastRng rng(stable_seed(cfg.master_seed, c));
            int64_t a_count = pareto_sample(rng, 1.0, 1.9, 6.0);

            for (int64_t a_idx = 0; a_idx < a_count; ++a_idx) {
                int64_t acct_id = cfg.start_account_id + (c - cfg.start_customer_id) * account_id_stride + a_idx;
                constexpr int annual_rate_basis_points = 350;
                constexpr double rate = annual_rate_basis_points / 10000.0;
                std::string acct_type = "flex";
                std::string acct_name = "Sparkonto Flex";
                std::string acct_num = std::format("NOR-{:06d}", acct_id);

                double opening_amount = static_cast<double>(rng.randint(15000, 180000));

                int64_t next_tx_id = cfg.start_ledger_id + (acct_id - cfg.start_account_id) * ledger_id_stride;
                int64_t opening_tx_id = next_tx_id++;
                append_ledger_row(ledger_buf, opening_tx_id, acct_id, "deposit", opening_amount, "Öppningsinsättning", opening_date);
                ++block.transactions;

                int64_t tx_count = pareto_sample(rng, 6.0, 1.45, 400.0);
                int64_t balance_cents = static_cast<int64_t>(opening_amount) * 100;
                // Generate activity in date order so withdrawals use the funds
                // actually available on that date, then accrue on daily balances.
                for (int m = 1; m <= 12; ++m) {
                    const int txs_in_month = static_cast<int>(tx_count / 12) + rng.randint(0, 2);
                    const auto month_opening_cents = balance_cents;
                    std::array<int64_t, 31> daily_changes{};
                    std::array<int, 35> activity_days; // At most 400 / 12 + 2 events.
                    for (int k = 0; k < txs_in_month; ++k)
                        activity_days[k] = static_cast<int>(rng.randint(1, month_days[m-1]));
                    std::sort(activity_days.begin(), activity_days.begin() + txs_in_month);
                    for (int k = 0; k < txs_in_month; ++k) {
                        const int day = activity_days[k];
                        const bool is_dep = (balance_cents < 500000) || (rng.next_double() < 0.45);
                        int64_t tx_id = next_tx_id++;
                        if (is_dep) {
                            const int64_t cents = std::llround((rng.randint(200, 35000) + rng.next_double()) * 100.0);
                            balance_cents += cents;
                            daily_changes[day-1] += cents;
                            append_ledger_row(ledger_buf, tx_id, acct_id, "deposit", cents / 100.0, deposit_label, deposit_dates[m-1][day-1]);
                        } else {
                            const double max_w = std::max(100.0, balance_cents * 0.007);
                            const int64_t cents = std::llround((rng.randint(50, static_cast<int64_t>(std::min(20000.0, max_w))) + rng.next_double()) * 100.0);
                            balance_cents -= cents;
                            daily_changes[day-1] -= cents;
                            append_ledger_row(ledger_buf, tx_id, acct_id, "withdrawal", -cents / 100.0, withdrawal_label, withdrawal_dates[m-1][day-1]);
                        }
                        ++block.transactions;
                    }

                    const auto gross_cents = monthly_interest_cents(month_opening_cents,
                        std::span(daily_changes.data(), month_days[m-1]), annual_rate_basis_points, days_in_year);
                    if (gross_cents > 0) {
                        const int64_t tax_cents = (gross_cents * 30 + 50) / 100;
                        int64_t int_id = next_tx_id++;
                        int64_t tax_id = next_tx_id++;
                        append_ledger_row(ledger_buf, int_id, acct_id, "interest", gross_cents / 100.0, "Ränteutbetalning", interest_dates[m-1]);
                        append_ledger_row(ledger_buf, tax_id, acct_id, "tax", -tax_cents / 100.0, "Preliminärskatt 30%", tax_dates[m-1]);
                        balance_cents += gross_cents - tax_cents;
                        block.transactions += 2;
                    }
                }

                std::format_to(std::back_inserter(acct_buf),
                    "{}\t{}\t{}\t{}\t{}\t{:.2f}\t{:.6f}\tactive\t{}-01-01 08:30:00+00\n",
                    acct_id, c, acct_num, acct_name, acct_type, balance_cents / 100.0, rate, cfg.year
                );

                if (next_tx_id > cfg.start_ledger_id + (acct_id - cfg.start_account_id + 1) * ledger_id_stride)
                    throw std::logic_error("Ledger workload exceeded reserved ID range");
                ++block.accounts;
            }
            ++block.customers;
        }
    }
    void worker() {
        try {
            for (;;) {
                std::unique_ptr<WorkBlock> block;
                int64_t offset, count;
                std::vector<std::string> identities;
                {
                    std::unique_lock lock(mutex);
                    changed.wait(lock, [&] { return stopping || (next < limit && !free.empty()); });
                    if (stopping) return;
                    offset = next; count = std::min<int64_t>(16, limit - next); next += count;
                    block = std::move(free.front()); free.pop_front();
                    if (appending) for (int64_t i = 0; i < count; ++i) identities.push_back(append_identities.next());
                }
                block->clear(); generate(*block, offset, count, identities);
                generated.fetch_add(count, std::memory_order_relaxed);
                {
                    std::lock_guard lock(mutex);
                    ready.push_back(std::move(block));
                }
                changed.notify_all();
            }
        } catch (...) {
            std::lock_guard lock(mutex); failure = std::current_exception(); stopping = true; changed.notify_all();
        }
    }
public:
    GenerationPool(const GeneratorConfig& config, PersonalNumberSequence& identities, bool append, int64_t role_id, std::atomic<int64_t>& generated_count)
        : cfg(config), append_identities(identities), appending(append), role(role_id), generated(generated_count),
          limit(std::min<int64_t>(500, cfg.customer_count)),
          days_in_year(std::chrono::year{cfg.year}.is_leap() ? 366 : 365),
          created_str(std::format("{}-01-01 08:00:00+00", cfg.year)),
          opening_date(std::format("{}-01-01 09:00:00+00", cfg.year)),
          deposit_label(cfg.stress_text ? "Lön från Volvo Group AB — referens " + std::string(160, 'A') + " åäö ÅÄÖ" : "Lön från Volvo Group AB"),
          withdrawal_label(cfg.stress_text ? "Kortköp på ICA Supermarket — referens " + std::string(160, 'B') + " åäö ÅÄÖ" : "Kortköp på ICA Supermarket") {
        for (int m = 1; m <= 12; ++m) {
            const auto month_end = std::chrono::year{cfg.year}/std::chrono::month{static_cast<unsigned>(m)}/std::chrono::last;
            month_days[m-1] = static_cast<unsigned>(month_end.day());
            for (int d = 1; d <= month_days[m-1]; ++d) {
                deposit_dates[m-1][d-1] = std::format("{}-{:02d}-{:02d} 12:00:00+00", cfg.year, m, d);
                withdrawal_dates[m-1][d-1] = std::format("{}-{:02d}-{:02d} 14:00:00+00", cfg.year, m, d);
            }
            interest_dates[m-1] = std::format("{}-{:02d}-{:02d} 23:59:50+00", cfg.year, m, month_days[m-1]);
            tax_dates[m-1] = std::format("{}-{:02d}-{:02d} 23:59:55+00", cfg.year, m, month_days[m-1]);
        }
        const auto workers = std::min<int64_t>({cfg.thread_count, cfg.customer_count, 64});
        for (int i = 0; i < workers * 2; ++i) free.push_back(std::make_unique<WorkBlock>());
        try {
            for (int i = 0; i < workers; ++i) threads.emplace_back([this] { worker(); });
        } catch (...) {
            // A throwing constructor does not run ~GenerationPool. Wake already
            // started workers before the jthread members join during unwinding.
            { std::lock_guard lock(mutex); stopping = true; }
            changed.notify_all();
            throw;
        }
    }
    std::unique_ptr<WorkBlock> take() {
        std::unique_lock lock(mutex);
        changed.wait(lock, [&] { return failure || !ready.empty(); });
        if (failure) std::rethrow_exception(failure);
        auto result = std::move(ready.front()); ready.pop_front(); return result;
    }
    void recycle(std::unique_ptr<WorkBlock> block) {
        std::lock_guard lock(mutex); free.push_back(std::move(block)); changed.notify_all();
    }
    void sustain() { std::lock_guard lock(mutex); limit = cfg.customer_count; changed.notify_all(); }
    ~GenerationPool() {
        { std::lock_guard lock(mutex); stopping = true; }
        changed.notify_all();
        threads.clear();
    }
};

static constexpr std::array<std::string_view, 4> copy_sql{
    R"(COPY banking.customers ("Id", "PersonalNum", "Name", "UserName", "NormalizedUserName", "Email", "NormalizedEmail", "PhoneNumber", "PasswordHash", "SecurityStamp", "ConcurrencyStamp", "EmailConfirmed", "PhoneNumberConfirmed", "TwoFactorEnabled", "LockoutEnabled", "AccessFailedCount", "CreatedAt") FROM STDIN;)",
    R"(COPY banking."AspNetUserRoles" ("UserId", "RoleId") FROM STDIN;)",
    R"(COPY banking.savings_accounts ("Id", "CustomerId", "AccountNumber", "AccountName", "AccountType", "Balance", "InterestRate", "Status", "CreatedAt") FROM STDIN;)",
    R"(COPY banking.ledger_entries ("Id", "AccountId", "Type", "Amount", "Label", "TargetAccountId", "IsPlanned", "CreatedAt") FROM STDIN;)"
};
static constexpr std::string_view sequences = R"(SELECT setval(pg_get_serial_sequence('banking.customers', 'Id'), COALESCE((SELECT MAX("Id") FROM banking.customers), 1));
SELECT setval(pg_get_serial_sequence('banking.savings_accounts', 'Id'), COALESCE((SELECT MAX("Id") FROM banking.savings_accounts), 1));
SELECT setval(pg_get_serial_sequence('banking.ledger_entries', 'Id'), COALESCE((SELECT MAX("Id") FROM banking.ledger_entries), 1));
)";

static void print_progress_bar(const GenerationProgress& value, bool benchmark) {
    // Completion includes finalization, even after the last data commit.
    const auto filled = std::clamp(static_cast<int>(value.fraction * 15), 0, 15);
    std::string bar;
    for (int i = 0; i < 15; ++i) bar += i < filled ? "█" : "░";
    std::cerr << std::format("\r\033[K[{}] [{}] {:5.1f}% | {} / {} ({:>10} customers/s) | ETA: {} | {}",
        benchmark ? "Benchmark" : "Customers ready", bar, std::min(value.fraction * 100, value.stage == "Complete" ? 100.0 : 99.9),
        format_num(value.completed), format_num(value.total), format_num(static_cast<int64_t>(value.customers_per_second)),
        value.eta_seconds < 0 ? "--" : std::format("{:.0f}s", value.eta_seconds), value.stage);
}
int generate_main(int argc, char** argv, const ProgressCallback& progress_callback) {
    GeneratorConfig cfg;
    std::string container = "nordiska-v2-db-1";
    int64_t customer_role_id = 2;
    std::string dbname = "nordiska_synthetic";
    bool benchmark_mode = false, keep_indexes = false;

    cfg.thread_count = static_cast<int>(std::thread::hardware_concurrency());
    if (cfg.thread_count <= 0) cfg.thread_count = 4;

    for (int i = 1; i < argc; ++i) {
        std::string arg = argv[i];
        if (arg == "--customers" && i + 1 < argc) {
            cfg.customer_count = std::stoll(argv[++i]);
        } else if (arg == "--threads" && i + 1 < argc) {
            cfg.thread_count = std::stoi(argv[++i]);
        } else if (arg == "--seed" && i + 1 < argc) {
            cfg.master_seed = std::stoull(argv[++i]);
        } else if (arg == "--year" && i + 1 < argc) {
            cfg.year = std::stoi(argv[++i]);
        } else if (arg == "--start-cust-id" && i + 1 < argc) {
            cfg.start_customer_id = std::stoll(argv[++i]);
        } else if (arg == "--start-acct-id" && i + 1 < argc) {
            cfg.start_account_id = std::stoll(argv[++i]);
        } else if (arg == "--start-tx-id" && i + 1 < argc) {
            cfg.start_ledger_id = std::stoll(argv[++i]);
        } else if (arg == "--container" && i + 1 < argc) {
            container = argv[++i];
        } else if (arg == "--db" && i + 1 < argc) {
            dbname = argv[++i];
        } else if (arg == "--customer-role-id" && i + 1 < argc) {
            customer_role_id = std::stoll(argv[++i]);
        } else if (arg == "--stress-text") {
            cfg.stress_text = true;
        } else if (arg == "--keep-indexes") {
            keep_indexes = true;
        } else if (arg == "--benchmark") {
            benchmark_mode = true;
        }
    }

    if (cfg.customer_count <= 0 || cfg.thread_count <= 0 || cfg.year < 2000 || cfg.year > 2100 || customer_role_id <= 0) {
        std::cerr << "Customer/thread counts and role ID must be positive; year must be 2000..2100.\n";
        return 1;
    }
    if (cfg.customer_count > static_cast<int64_t>(personal_number_capacity)) {
        std::cerr << "Personal-number capacity exhausted: maximum 37,620,000 customers (1905-01-01 through 2007-12-31).\n";
        return 1;
    }

    std::cout << std::format("\033[1;36mNordiska Fast Native Generator (C++23)\033[0m\n");
    std::cout << std::format("  Customers:    {:>12}\n", format_num(cfg.customer_count));
    std::cout << std::format("  CPU Threads:  {:>12}\n", cfg.thread_count);
    std::cout << std::format("  Target DB:    {:>12}\n", dbname);
    std::cout << std::format("  Mode:         {:>12}\n\n", benchmark_mode ? "Benchmark (Dry-run)" : "Direct PostgreSQL COPY Stream");
    std::cout.flush();

    using Clock = std::chrono::steady_clock;
    const auto start_time = Clock::now();
    // Creation identities are derived directly from their ordinal. Only append
    // needs the fixed bitmap to skip previously occupied identities.
    PersonalNumberSequence append_identities(cfg.master_seed);
    const bool appending = !benchmark_mode && cfg.start_customer_id > 1;
    // Appending must not regenerate identities that already belong to customers.
    if (appending) {
        auto query = "docker exec " + shell_quote(container) +
            " psql -U nordiska_bootstrap -d " + shell_quote(dbname) +
            " -v ON_ERROR_STOP=1 -At -c " + shell_quote("SELECT \"PersonalNum\" FROM banking.customers;");
        FILE* existing = popen(query.c_str(), "r");
        if (!existing) throw std::runtime_error("Cannot read existing customer identities");
        char line[128];
        while (fgets(line, sizeof(line), existing)) {
            std::string value(line);
            value.erase(value.find_last_not_of("\r\n") + 1);
            append_identities.reserve(value);
        }
        if (pclose(existing) != 0) throw std::runtime_error("Cannot read existing customer identities");
        if (static_cast<uint64_t>(cfg.customer_count) > append_identities.remaining())
            throw std::runtime_error("Personal-number capacity exhausted: only " +
                format_num(append_identities.remaining()) + " identities remain; no customers appended");
    }

    std::atomic<int64_t> generated_count{0};
    struct RunProgress {
        std::mutex mutex;
        int64_t ready = 0, transactions = 0, transferred = 0;
        double rate = 0, finalization_reserve = 0;
        std::string stage = "Preparing ingestion session";
        bool complete = false;
        Clock::time_point measured = Clock::now();
        int64_t measured_ready = 0;
    } state;
    std::mutex report_mutex;
    auto report = [&] {
        std::lock_guard output_guard(report_mutex);
        GenerationProgress value;
        {
            std::lock_guard guard(state.mutex);
            const auto now = Clock::now();
            const auto window = std::chrono::duration<double>(now - state.measured).count();
            // Measurement clock is independent of scheduling, COPY flushes and
            // commits. A time-weighted EWMA smooths commit bursts and slowdowns.
            if (state.rate > 0 && window >= 1.0) {
                const auto sample = (state.ready - state.measured_ready) / window;
                const auto weight = 1 - std::exp(-window / 4.0);
                state.rate += weight * (sample - state.rate);
                state.measured = now; state.measured_ready = state.ready;
            }
            value.stage = state.stage;
            value.customers = value.completed = state.ready;
            value.generated_customers = generated_count.load(std::memory_order_relaxed);
            value.transferred_customers = state.transferred;
            value.transactions = state.transactions;
            value.total = cfg.customer_count;
            value.fraction = state.complete ? 1.0 : std::min(0.999, double(state.ready) / cfg.customer_count);
            value.customers_per_second = state.rate;
            if (state.rate > 0) value.eta_seconds = state.complete ? 0 :
                std::max(1.0, (cfg.customer_count - state.ready) / state.rate + state.finalization_reserve);
            if (!state.complete && state.ready == cfg.customer_count) value.eta_seconds = -1;
        }
        if (progress_callback) progress_callback(value);
        else print_progress_bar(value, benchmark_mode);
    };
    auto set_stage = [&](std::string stage) { std::lock_guard guard(state.mutex); state.stage = std::move(stage); };
    std::jthread reporter([&](std::stop_token stop) {
        while (!stop.stop_requested()) {
            report(); std::this_thread::sleep_for(std::chrono::milliseconds(100));
        }
    });
    DeferredLedgerIndexes deferred_indexes;
    try {
        int64_t serial = 0;
        std::unique_ptr<PsqlSession> session;
        if (!benchmark_mode) {
            session = std::make_unique<PsqlSession>(container, dbname);
            session->barrier(0);
            if (!keep_indexes && cfg.customer_count >= 10000 && cfg.start_customer_id == 1)
                deferred_indexes.discover(*session, serial);
        }
        const auto setup_seconds = std::chrono::duration<double>(Clock::now() - start_time).count();
        const auto generation_started = Clock::now();
        GenerationPool pool(cfg, append_identities, appending, customer_role_id, generated_count);
        set_stage("Calibrating first " + format_num(std::min<int64_t>(500, cfg.customer_count)) + " customers");
        WorkBlock transaction;
        int64_t consumed = 0, accounts = 0, data_commits = 0;
        auto transaction_started = Clock::now();
        int64_t row_budget = 50000;
        constexpr size_t byte_budget = 8 * 1024 * 1024;
        bool initial = true;
        double transfer_seconds = 0, commit_seconds = 0, finalization_seconds = 0;
        auto commit = [&] {
            if (transaction.customers == 0) return;
            const auto began = Clock::now();
            if (session) {
                set_stage(initial ? "Saving initial sample" : "Transferring data");
                session->write("BEGIN;\n");
                for (size_t table = 0; table < copy_sql.size(); ++table) {
                    session->write(copy_sql[table]); session->write("\n");
                    session->write(transaction.rows[table]); session->write("\\.\n");
                }
                session->flush();
                {
                    std::lock_guard guard(state.mutex);
                    state.transferred += transaction.customers;
                }
                transfer_seconds += std::chrono::duration<double>(Clock::now() - began).count();
                set_stage("Waiting for database commit");
                if (initial) session->write(sequences);
                session->write("COMMIT;\n");
                session->barrier(++serial);
                ++data_commits;
            }
            const auto ended = Clock::now();
            const auto seconds = std::chrono::duration<double>(ended - began).count();
            commit_seconds += seconds;
            accounts += transaction.accounts;
            {
                std::lock_guard guard(state.mutex);
                state.ready += transaction.customers;
                state.transactions += transaction.transactions;
                if (initial) {
                    const auto sample_seconds = std::chrono::duration<double>(ended - generation_started).count();
                    state.rate = state.ready / std::max(sample_seconds, 0.000001);
                    // Approximate bulk-index/finalization reserve derived from
                    // this machine's fully indexed calibration, not a fixed rate.
                    if (deferred_indexes.size()) state.finalization_reserve = 0.2 * cfg.customer_count / state.rate;
                    state.measured = ended; state.measured_ready = state.ready;
                }
                state.stage = "Generating and saving";
            }
            // Aim for roughly one second per data transaction, constrained by
            // row and byte targets, rounded to whole work blocks. The initial
            // sample is the sole fixed customer boundary.
            if (session && !initial) {
                const auto target = transaction.transactions / std::max(0.01, seconds);
                row_budget = std::clamp<int64_t>(static_cast<int64_t>(0.7 * row_budget + 0.3 * target), 10000, 100000);
            }
            transaction.clear();
            transaction_started = Clock::now();
            report();
            if (initial) {
                if (session && deferred_indexes.size()) {
                    set_stage("Preparing ledger query indexes for bulk loading");
                    deferred_indexes.defer(*session, serial);
                }
                initial = false; pool.sustain();
            }
        };
        while (consumed < cfg.customer_count) {
            auto block = pool.take();
            // Never split a customer's related records across transactions.
            if (!initial && transaction.customers &&
                (transaction.transactions + block->transactions > row_budget || transaction.bytes() + block->bytes() > byte_budget ||
                 Clock::now() - transaction_started >= std::chrono::seconds(1)))
                commit();
            for (size_t table = 0; table < transaction.rows.size(); ++table) transaction.rows[table] += block->rows[table];
            transaction.customers += block->customers;
            transaction.accounts += block->accounts;
            transaction.transactions += block->transactions;
            consumed += block->customers;
            {
                std::lock_guard guard(state.mutex);
                if (!session) state.transferred = consumed;
            }
            pool.recycle(std::move(block));
            if ((initial && consumed == std::min<int64_t>(500, cfg.customer_count)) || consumed == cfg.customer_count)
                commit();
        }
        const auto final_started = Clock::now();
        if (session) {
            deferred_indexes.restore(*session, serial, [&](size_t index, size_t total) {
                set_stage("Rebuilding ledger query index " + std::to_string(index + 1) + "/" + std::to_string(total));
            });
            set_stage("Updating sequences and database statistics");
            session->write(sequences);
            session->write("ANALYZE banking.customers; ANALYZE banking.\"AspNetUserRoles\"; ANALYZE banking.savings_accounts; ANALYZE banking.ledger_entries;\n");
            session->barrier(++serial);
            session->close();
        }
        finalization_seconds = std::chrono::duration<double>(Clock::now() - final_started).count();
        {
            std::lock_guard guard(state.mutex); state.complete = true; state.stage = "Complete";
        }
        reporter.request_stop(); reporter.join(); report();
        const auto total_seconds = std::chrono::duration<double>(Clock::now() - start_time).count();
        std::cout << std::format("\n✓ {} {:>12} customers, {:>12} accounts and {:>12} transactions in {:.2f}s\n",
            benchmark_mode ? "Generated" : "Inserted", format_num(cfg.customer_count), format_num(accounts), format_num(state.transactions), total_seconds);
        std::cout << std::format("  Setup {:.3f}s | transfer/write {:.3f}s | ingest including transfer/commit {:.3f}s | finalization {:.3f}s | {} commits\n",
            setup_seconds, transfer_seconds, commit_seconds, finalization_seconds, data_commits);
        return 0;
    } catch (const std::exception& error) {
        reporter.request_stop(); reporter.join();
        std::cerr << "\n" << error.what() << "; only acknowledged commits count as ready.\n";
        if (deferred_indexes.pending()) {
            try {
                std::cerr << "Restoring deferred ledger query indexes after failure...\n";
                PsqlSession repair(container, dbname);
                int64_t repair_serial = 0;
                deferred_indexes.restore(repair, repair_serial);
                repair.close();
                std::cerr << "Ledger query indexes restored; the dataset is still partial.\n";
            } catch (const std::exception& repair_error) {
                std::cerr << "Ledger index restoration failed: " << repair_error.what()
                          << "; rerun a replacement seed before using this database.\n";
            }
        }
        return 1;
    }
}
