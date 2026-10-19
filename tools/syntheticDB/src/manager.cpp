#include <algorithm>
#include <array>
#include <cerrno>
#include <csignal>
#include <clocale>
#include <cstdio>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <iomanip>
#include <limits>
#include <mutex>
#include <cmath>
#include <regex>
#include <sstream>
#include <stdexcept>
#include <string>
#include <thread>
#include <vector>
#include <fcntl.h>
#include <sys/file.h>
#include <sys/wait.h>
#include <unistd.h>
#include "terminal_menu.hpp"
#include "fixture_login.hpp"
#include "generation_progress.hpp"
#include "generator.hpp"

namespace fs = std::filesystem;
using Args = std::vector<std::string>;

static std::string trim(std::string value) {
    const auto first = value.find_first_not_of(" \t\r\n");
    if (first == std::string::npos) return {};
    return value.substr(first, value.find_last_not_of(" \t\r\n") - first + 1);
}
static std::string quote(const std::string& value) {
    std::string result = "'";
    for (char c : value) result += c == '\'' ? "'\\''" : std::string(1, c);
    return result + "'";
}
static std::string command(const Args& args) {
    std::string result;
    for (const auto& arg : args) result += (result.empty() ? "" : " ") + quote(arg);
    return result;
}
static std::string read_file(const fs::path& path) {
    std::ifstream stream(path);
    if (!stream) throw std::runtime_error("Cannot read " + path.string());
    return {std::istreambuf_iterator<char>(stream), std::istreambuf_iterator<char>()};
}
// Private temporary files keep schema dumps and environment rewrites off shared paths.
static fs::path default_temp_pattern() {
    if (fs::exists("tmp") && fs::is_directory("tmp")) {
        return "tmp/nordiska-synthetic.XXXXXX";
    }
    return fs::temp_directory_path() / "nordiska-synthetic.XXXXXX";
}

struct TempFile {
    fs::path path;
    explicit TempFile(fs::path pattern = default_temp_pattern()) {
        std::string name = pattern.string();
        int fd = mkstemp(name.data());
        if (fd < 0) throw std::runtime_error("Cannot create temporary file");
        close(fd);
        path = name;
    }
    void write(const std::string& value) const {
        std::ofstream stream(path, std::ios::binary | std::ios::trunc);
        stream << value;
        stream.close();
        if (!stream) throw std::runtime_error("Cannot write temporary file");
    }
    ~TempFile() { std::error_code error; fs::remove(path, error); }
};
static std::string run(const Args& args, const std::string* input = nullptr) {
    TempFile stdin_file;
    std::string cmd = command(args);
    if (input) { stdin_file.write(*input); cmd += " < " + quote(stdin_file.path.string()); }
    FILE* pipe = popen(cmd.c_str(), "r");
    if (!pipe) throw std::runtime_error("Cannot start command: " + command(args));
    std::string output;
    char buffer[8192];
    while (size_t count = fread(buffer, 1, sizeof(buffer), pipe)) output.append(buffer, count);
    bool read_error = ferror(pipe);
    int status = pclose(pipe);
    if (read_error || status == -1 || !WIFEXITED(status) || WEXITSTATUS(status) != 0)
        throw std::runtime_error("Command failed: " + command(args));
    return trim(output);
}
static fs::path repo_root() {
    // Locate the checkout from the executable directory.
    fs::path current = fs::read_symlink("/proc/self/exe").parent_path();
    while (!current.empty()) {
        if (fs::exists(current / "infra/docker-compose.yml")) return current;
        auto parent = current.parent_path();
        if (parent == current) break;
        current = parent;
    }
    throw std::runtime_error("Run the checked-in executable inside the repository checkout");
}
static bool valid_database(const std::string& name) {
    return name == "nordiska_v2" || name == "nordiska_synthetic";
}
static std::string document_volume(const std::string& database) {
    return database == "nordiska_synthetic" ? "nordiska-v2_synthetic_report_documents" : "nordiska-v2_report_documents";
}
static std::string database_label(const std::string& database) {
    if (database == "nordiska_v2") return "NORMAL (nordiska_v2)";
    if (database == "nordiska_synthetic") return "SYNTHETIC (nordiska_synthetic)";
    return database;
}
static std::string format_count(std::string value) {
    if (value.empty() || !std::all_of(value.begin(), value.end(), [](char ch) { return ch >= '0' && ch <= '9'; }))
        throw std::runtime_error("Invalid database count");
    for (int position = static_cast<int>(value.size()) - 3; position > 0; position -= 3) value.insert(position, ",");
    return value;
}
static fs::path local_env(const fs::path& infra) {
    auto env = infra / ".env";
    if (!fs::is_regular_file(env)) env = infra / "v2/.env";
    if (!fs::is_regular_file(env)) throw std::runtime_error("infra/.env is missing (legacy infra/v2/.env was also checked); run DevSetup first");
    return env;
}

struct OperationLock {
    int fd;
    explicit OperationLock(const fs::path& path) : fd(open(path.c_str(), O_CREAT | O_RDWR | O_NOFOLLOW, 0600)) {
        if (fd < 0) throw std::runtime_error("Cannot open synthetic tool lock");
        if (flock(fd, LOCK_EX | LOCK_NB) != 0) {
            close(fd);
            throw std::runtime_error("Another synthetic data manager is running in this checkout");
        }
    }
    ~OperationLock() { close(fd); }
    OperationLock(const OperationLock&) = delete;
    OperationLock& operator=(const OperationLock&) = delete;
};

class DatabaseManager {
    fs::path infra, env;
    OperationLock lock;
    std::vector<fs::path> files;
    size_t worker_count = 1;
    std::string app_service = "app";
    static inline const std::regex selection{R"(^[ \t]*NORDISKA_DATABASE[ \t]*=[ \t]*(.*)$)"};
    std::vector<std::string> services(const std::string& name) {
        std::istringstream rows(compose({"ps", "--quiet", name == "app" ? app_service : name}));
        std::vector<std::string> result;
        std::string id;
        while (rows >> id) result.push_back(id);
        return result;
    }
    std::string service(const std::string& name) {
        auto ids = services(name);
        if (ids.size() > 1) throw std::runtime_error("Expected one " + name + " container in nordiska-v2");
        return ids.empty() ? "" : ids.front();
    }
    std::string runtime_database(const std::string& id) const {
        // Parse in memory: connection strings and their passwords must never be printed.
        auto values = run({"docker", "inspect", "--format", "{{range .Config.Env}}{{println .}}{{end}}", id});
        std::istringstream lines(values);
        std::string line;
        std::regex database{R"((?:^|;)Database=([^;]+))", std::regex::icase};
        while (std::getline(lines, line)) {
            if (line.rfind("ConnectionStrings__ReportingDatabase=", 0) != 0) continue;
            std::smatch match;
            auto connection = line.substr(line.find('=') + 1);
            if (std::regex_search(connection, match, database)) return match[1];
        }
        return "unknown";
    }
    bool runtime_development(const std::string& id) const {
        std::istringstream lines(run({"docker", "inspect", "--format", "{{range .Config.Env}}{{println .}}{{end}}", id}));
        std::string line, environment = "Production", dotnet_environment;
        while (std::getline(lines, line)) {
            if (line.starts_with("ASPNETCORE_ENVIRONMENT=")) environment = line.substr(23);
            if (line.starts_with("DOTNET_ENVIRONMENT=")) dotnet_environment = line.substr(19);
        }
        if (!dotnet_environment.empty()) environment = dotnet_environment;
        return std::regex_match(environment, std::regex("Development", std::regex::icase));
    }
    struct PdfStorage {
        std::string type, name, source, destination, access;
    };
    PdfStorage runtime_storage(const std::string& id) const {
        auto mounts = run({"docker", "inspect", "--format",
            "{{range .Mounts}}{{printf \"%s\\t%s\\t%s\\t%s\\t%t\\n\" .Type .Name .Source .Destination .RW}}{{end}}", id});
        std::istringstream rows(mounts);
        std::string row;
        while (std::getline(rows, row)) {
            std::istringstream columns(row);
            PdfStorage storage;
            std::getline(columns, storage.type, '\t');
            std::getline(columns, storage.name, '\t');
            std::getline(columns, storage.source, '\t');
            std::getline(columns, storage.destination, '\t');
            std::getline(columns, storage.access, '\t');
            if (storage.destination == "/var/lib/nordiska/report-documents") return storage;
        }
        return {};
    }
    bool synthetic_volume_exists() const {
        std::istringstream volumes(run({"docker", "volume", "ls", "--quiet"}));
        std::string name;
        while (std::getline(volumes, name)) if (name == document_volume("nordiska_synthetic")) return true;
        return false;
    }
    std::string document_volume_command(bool readonly, const std::string& script) const {
        const auto image = run({"docker", "inspect", "--format", "{{.Image}}", container});
        // Fixed synthetic volume only; never accept a path or a volume from .env.
        return run({"docker", "run", "--rm", "--pull=never", "--network", "none", "--read-only", "--user", "0",
            "--mount", "type=volume,src=" + document_volume("nordiska_synthetic") +
                ",dst=/synthetic-documents,volume-nocopy" + (readonly ? ",readonly" : ""),
            "--entrypoint", "sh", image, "-c", script});
    }
    bool synthetic_files_present() const {
        if (!synthetic_volume_exists()) return false;
        return document_volume_command(true, "files=$(find /synthetic-documents -mindepth 1 ! -type d -printf x -quit) || exit 1; "
            "[ -z \"$files\" ] && printf 0 || printf 1") == "1";
    }
    void write_env(const std::string& content) {
        TempFile replacement(env.parent_path() / ".env.synthetic.XXXXXX");
        replacement.write(content);
        fs::permissions(replacement.path, fs::status(env).permissions());
        fs::rename(replacement.path, env);
    }
    std::array<std::string, 5> database_counts(const std::string& database) const {
        const std::string query = R"sql(SELECT (SELECT COUNT(*) FROM banking.customers) || '|' ||
          (SELECT COUNT(*) FROM banking.savings_accounts) || '|' ||
          (SELECT COUNT(*) FROM banking.ledger_entries) || '|' ||
          (SELECT COUNT(*) FROM reporting.generated_documents) || '|' ||
          pg_size_pretty(pg_database_size(current_database()));)sql";
        std::istringstream values(sql(database, query));
        std::array<std::string, 5> counts;
        for (auto& count : counts)
            if (!std::getline(values, count, '|')) throw std::runtime_error("Cannot read database contents: " + database);
        return counts;
    }
    std::string database_contents(const std::string& database) const {
        auto counts = database_counts(database);
        return "Customers: " + format_count(counts[0]) + " | Accounts: " + format_count(counts[1]) +
               " | Transactions: " + format_count(counts[2]) + " | Documents: " + format_count(counts[3]) +
               " | Size: " + counts[4];
    }
    void recreate() {
        compose({"up", "-d", "--no-deps", "--force-recreate", "--wait", "--wait-timeout", "180",
                 "--scale", "reporting-worker=" + std::to_string(worker_count),
                 app_service, "reporting-worker", "pdf-signer"});
        for (auto name : {"app", "reporting-worker", "pdf-signer"}) {
            auto ids = services(name);
            if (ids.empty()) throw std::runtime_error(std::string(name) + " did not start");
            if (std::string(name) == "reporting-worker" && ids.size() != worker_count)
                throw std::runtime_error("Reporting worker count changed during database switching");
            for (const auto& id : ids)
                if (runtime_database(id) != active())
                    throw std::runtime_error(std::string(name) + " did not apply the database selection");
        }
    }
public:
    struct MenuState {
        std::string database = "unknown";
        std::string start_database = "nordiska_v2";
        bool app_running = false, synthetic_exists = false, login_available = false, documents_available = false;
        std::array<std::string, 5> normal{"-", "-", "-", "-", "-"}, synthetic{"-", "-", "-", "-", "-"};
    };
    MenuState menu_state() {
        MenuState state;
        auto app = service("app");
        state.app_running = !app.empty();
        if (!app.empty()) state.database = runtime_database(app);
        else state.database = "offline";
        state.normal = database_counts("nordiska_v2");
        state.synthetic_exists = exists();
        if (state.synthetic_exists) {
            state.synthetic = database_counts("nordiska_synthetic");
        }
        state.documents_available = (state.synthetic_exists && state.synthetic[3] != "0") || synthetic_files_present();
        if (!state.app_running) {
            state.start_database = active();
            if (state.start_database == "nordiska_synthetic" && !state.synthetic_exists)
                state.start_database = "nordiska_v2";
        }
        if (state.app_running && valid_database(state.database)) {
            state.login_available = sql(state.database, "SELECT EXISTS (SELECT 1 FROM banking.customers c WHERE " +
                fixture_customer_filter(runtime_development(app)) + ");") == "t";
        }
        return state;
    }
    bool needs_normal_database() {
        bool selected = active() == "nordiska_synthetic";
        for (auto name : {"app", "reporting-worker", "pdf-signer"}) {
            for (const auto& id : services(name)) {
                auto database = runtime_database(id);
                if (!valid_database(database)) throw std::runtime_error("Cannot verify the database used by " + std::string(name));
                selected = selected || database == "nordiska_synthetic";
            }
        }
        return selected;
    }
    struct StatusSummary {
        std::string app_database, worker_pdf_location;
        bool pending;
    };
    std::string container;
    DatabaseManager() : infra(repo_root() / "infra"), env(local_env(infra)),
                        lock(infra.parent_path() / "tools/syntheticDB/.synthetic-data.lock"),
                        files{infra / "docker-compose.yml", infra / "docker-compose.override.yml"} {
        container = service("db");
        if (container.empty()) throw std::runtime_error("The nordiska-v2 db service is not running; run DevSetup start-stack first");
        auto app = service("app");
        if (app.empty()) {
            app = run({"docker", "ps", "--filter", "label=com.docker.compose.project=nordiska-v2",
                       "--filter", "label=com.docker.compose.service=api", "--format", "{{.ID}}"});
            if (!app.empty()) app_service = "api";
        }
        if (!app.empty()) {
            auto labels = run({"docker", "inspect", "--format",
                "{{index .Config.Labels \"com.docker.compose.project.config_files\"}}", app});
            std::istringstream paths(labels);
            std::string path;
            while (std::getline(paths, path, ','))
                if (fs::path(trim(path)).lexically_normal() == infra / "docker-compose.frontend-local.yml")
                    files = {infra / "docker-compose.yml", infra / "docker-compose.frontend-local.yml"};
        }
        worker_count = std::max(size_t{1}, services("reporting-worker").size());
    }
    std::string active() const {
        std::istringstream lines(read_file(env));
        std::string line;
        std::smatch match;
        while (std::getline(lines, line)) if (std::regex_match(line, match, selection)) {
            auto value = trim(match[1]);
            // Compose permits a trailing comment after whitespace, and quoted values.
            auto comment = value.find(" #");
            if (comment != std::string::npos) value = trim(value.substr(0, comment));
            if (value.size() >= 2 && ((value.front() == '\'' && value.back() == '\'') ||
                                     (value.front() == '"' && value.back() == '"')))
                value = value.substr(1, value.size() - 2);
            if (!valid_database(value)) throw std::runtime_error("Unsupported NORDISKA_DATABASE selection");
            return value;
        }
        return "nordiska_v2";
    }
    void select(const std::string& database) {
        if (!valid_database(database)) throw std::runtime_error("Unsupported database: " + database);
        std::string content = read_file(env), output, line;
        std::istringstream lines(content);
        bool found = false, volume_found = false;
        const std::regex volume_selection{R"(^[ \t]*NORDISKA_REPORT_DOCUMENTS_VOLUME[ \t]*=.*$)"};
        while (std::getline(lines, line)) {
            if (std::regex_match(line, selection)) { line = "NORDISKA_DATABASE=" + database; found = true; }
            if (std::regex_match(line, volume_selection)) {
                line = "NORDISKA_REPORT_DOCUMENTS_VOLUME=" + document_volume(database);
                volume_found = true;
            }
            output += line + '\n';
        }
        if (!found) output = "NORDISKA_DATABASE=" + database + '\n' + output;
        if (!volume_found) output += "NORDISKA_REPORT_DOCUMENTS_VOLUME=" + document_volume(database) + '\n';
        write_env(output);
    }
    std::string compose(const Args& tail) const {
        // Shell exports must not override the selection saved in .env.
        if (setenv("NORDISKA_DATABASE", active().c_str(), 1) != 0)
            throw std::runtime_error("Cannot set database selection for Compose");
        if (setenv("NORDISKA_REPORT_DOCUMENTS_VOLUME", document_volume(active()).c_str(), 1) != 0)
            throw std::runtime_error("Cannot set document volume for Compose");
        Args args{"docker", "compose", "--project-name", "nordiska-v2", "--project-directory", infra.string(),
                  "--env-file", env.string()};
        for (const auto& file : files) { args.push_back("-f"); args.push_back(file.string()); }
        auto overrides = read_file(infra.parent_path() / "tools/syntheticDB/compose.override.yml");
        if (app_service == "api") {
            const auto key = overrides.find("\n  app:");
            if (key == std::string::npos) throw std::runtime_error("Missing app service in tool Compose override");
            overrides.replace(key, 7, "\n  api:");
        }
        TempFile override_file;
        override_file.write(overrides);
        args.push_back("-f");
        args.push_back(override_file.path.string());
        args.insert(args.end(), tail.begin(), tail.end());
        return run(args);
    }
    std::string sql(const std::string& database, const std::string& text) const {
        return run({"docker", "exec", "-i", container, "psql", "-U", "nordiska_bootstrap", "-d", database,
                    "-v", "ON_ERROR_STOP=1", "-A", "-t", "-c", text});
    }
    bool exists(const std::string& database = "nordiska_synthetic") const {
        if (!valid_database(database)) throw std::runtime_error("Unsupported database: " + database);
        return sql("postgres", "SELECT 1 FROM pg_database WHERE datname = '" + database + "';") == "1";
    }
    void switch_to(const std::string& target) {
        if (!valid_database(target)) throw std::runtime_error("Unsupported database: " + target);
        if (!exists(target)) throw std::runtime_error(target + " does not exist; prepare it first");
        auto previous_env = read_file(env);
        auto app = service("app");
        auto previous_database = app.empty() ? active() : runtime_database(app);
        if (!valid_database(previous_database)) previous_database = active();
        select(target);
        try { recreate(); }
        catch (const std::exception&) {
            write_env(previous_env);
            try {
                // A stale .env is not evidence of the previously running app's DB.
                if (active() != previous_database) select(previous_database);
                recreate();
                write_env(previous_env);
            }
            catch (const std::exception&) {
                write_env(previous_env);
                throw std::runtime_error("Switch and rollback failed; previous .env selection restored. Recreate the V2 services before continuing");
            }
            throw std::runtime_error("Database switch failed; previous services restored");
        }
        std::cout << "App, worker and signer now use " << target << '\n'
                  << "PDF volume: " << document_volume(target) << '\n';
    }
    void drop() {
        if (needs_normal_database()) switch_to("nordiska_v2");
        sql("postgres", "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = 'nordiska_synthetic';");
        sql("postgres", "DROP DATABASE IF EXISTS nordiska_synthetic;");
    }
    void delete_documents() {
        const auto volume = document_volume("nordiska_synthetic");
        std::vector<std::string> known, stop, stopped;
        // Reject storage mismatches before stopping services or deleting records.
        for (const auto name : {"app", "reporting-worker", "pdf-signer"}) {
            for (const auto& id : services(name)) {
                known.push_back(id);
                const auto database = runtime_database(id);
                const auto storage = runtime_storage(id);
                if (!valid_database(database)) throw std::runtime_error("Cannot verify service database before document cleanup");
                if (storage.name == volume && database != "nordiska_synthetic")
                    throw std::runtime_error("NORMAL service is using synthetic PDF storage; cleanup refused");
                if (database == "nordiska_synthetic") {
                    if (storage.type != "volume" || storage.name != volume)
                        throw std::runtime_error("Synthetic service PDF storage does not match its dedicated volume; cleanup refused");
                    stop.push_back(id);
                }
            }
        }
        std::istringstream users(run({"docker", "ps", "--quiet", "--no-trunc", "--filter", "volume=" + volume}));
        std::string id;
        while (users >> id)
            if (std::find(known.begin(), known.end(), id) == known.end())
                throw std::runtime_error("Another container is using synthetic PDF storage; cleanup refused");
        std::string failure;
        try {
            // Stop the app first, workers second, signer last; restart the same
            // containers without applying a possibly stale .env selection.
            for (const auto& service_id : stop) {
                stopped.push_back(service_id);
                run({"docker", "stop", "--time", "30", service_id});
            }
            if (exists()) {
                sql("nordiska_synthetic", R"sql(BEGIN;
                  SET LOCAL lock_timeout = '5s';
                  LOCK TABLE reporting.account_statement_jobs, reporting.tax_report_jobs,
                    reporting.account_statement_documents, reporting.tax_report_documents,
                    reporting.generated_documents IN ACCESS EXCLUSIVE MODE;
                  DELETE FROM reporting.account_statement_documents;
                  DELETE FROM reporting.tax_report_documents;
                  DELETE FROM reporting.account_statement_jobs;
                  DELETE FROM reporting.tax_report_jobs;
                  DELETE FROM reporting.generated_documents;
                  COMMIT;)sql");
            }
            if (synthetic_volume_exists()) {
                document_volume_command(false, "find /synthetic-documents -mindepth 1 -maxdepth 1 -exec rm -rf -- {} +");
                if (synthetic_files_present()) throw std::runtime_error("Synthetic PDF storage is not empty; retry cleanup");
            }
        } catch (const std::exception& error) { failure = error.what(); }
        std::string restart_failure;
        for (const auto& service_id : stopped) {
            try {
                if (run({"docker", "inspect", "--format", "{{.State.Running}}", service_id}) != "true")
                    run({"docker", "start", service_id});
            } catch (const std::exception&) { restart_failure += " " + service_id; }
        }
        if (!restart_failure.empty()) throw std::runtime_error("Document cleanup finished, but services could not restart:" + restart_failure +
            (failure.empty() ? "" : ". Cleanup error: " + failure));
        if (!failure.empty()) throw std::runtime_error("Document cleanup failed; previous containers restarted. " + failure);
    }
    std::string dump(const Args& options) {
        Args args{"docker", "exec", container, "pg_dump", "-U", "nordiska_bootstrap"};
        args.insert(args.end(), options.begin(), options.end());
        args.push_back("nordiska_v2");
        auto output = run(args);
        if (output.empty()) throw std::runtime_error("Source database dump was empty; synthetic data was preserved");
        return output;
    }
    void restore(const std::string& content) {
        run({"docker", "exec", "-i", container, "psql", "-U", "nordiska_bootstrap", "-d", "nordiska_synthetic",
             "-v", "ON_ERROR_STOP=1", "-q"}, &content);
    }
    void reset() {
        // Preflight both source dumps before replacing an existing synthetic database.
        auto schema = dump({"--schema-only"});
        auto reference = dump({"--data-only", "-t", "banking.account_type_configs", "-t", "banking.\"AspNetRoles\"", "-t", "faq.faq_entries"});
        drop();
        sql("postgres", "CREATE DATABASE nordiska_synthetic OWNER nordiska_bootstrap;");
        restore(schema);
        restore(reference);
        sql("postgres", "GRANT CONNECT ON DATABASE nordiska_synthetic TO nordiska_migrator, nordiska_api, nordiska_reporting_worker;");
        run({"docker", "exec", container, "psql", "-U", "nordiska_bootstrap", "-d", "nordiska_synthetic",
             "-v", "ON_ERROR_STOP=1", "-f", "/opt/nordiska/post-migration-permissions.sql"});
    }
    static std::string fixture_customer_filter(bool development) {
        // Only accounts with a known fixture password or the app's Development
        // demo login are eligible. Never infer plaintext from arbitrary hashes.
        auto generated = std::string(R"sql((c."Email" = 'cust' || c."Id"::text || '@nordiska-demo.se'
          AND c."PasswordHash" = ')sql") + std::string(nordiska::synthetic::fixture_password_hash) + R"sql('
          AND EXISTS (SELECT 1 FROM banking."AspNetUserRoles" ur JOIN banking."AspNetRoles" r ON r."Id" = ur."RoleId"
              WHERE ur."UserId" = c."Id" AND r."NormalizedName" = 'CUSTOMER')))sql";
        const auto demo = development ? R"sql( OR (COALESCE(c."PasswordHash", '') = '' AND (
          (c."Email" = 'anna@example.com' AND c."PersonalNum" = '198202116050') OR
          (c."Email" = 'erik@example.com' AND c."PersonalNum" = '197903142380'))))sql" : "";
        return "(" + generated + demo + R"sql()
          AND (NOT c."LockoutEnabled" OR c."LockoutEnd" IS NULL OR c."LockoutEnd" <= NOW())
          AND EXISTS (SELECT 1 FROM banking.savings_accounts a WHERE a."CustomerId" = c."Id")
          AND NOT EXISTS (SELECT 1 FROM banking.staff_members s WHERE s."IsActive" AND LOWER(s."Email") = LOWER(c."Email")))sql";
    }
    struct CustomerLogin {
        std::string database, details;
        std::string title() const {
            return "Random customer login from " + std::string(database == "nordiska_synthetic" ? "SYNTHETIC" : "NORMAL") + " DB";
        }
    };
    CustomerLogin get_random_customer() {
        auto app = service("app");
        if (app.empty()) throw std::runtime_error("App is not running; no active database for customer login");
        const auto database = runtime_database(app);
        if (!valid_database(database)) throw std::runtime_error("Cannot determine the active app database for customer login");
        const auto query = std::string(R"sql(SELECT 'Customer ID: ' || c."Id" || E'\nName: ' || c."Name" ||
          E'\nEmail: ' || c."Email" || E'\nPassword: )sql") + std::string(nordiska::synthetic::fixture_password) + R"sql(' ||
          E'\nPersonal number: ' || c."PersonalNum" FROM banking.customers c WHERE )sql" +
          fixture_customer_filter(runtime_development(app)) + " ORDER BY random() LIMIT 1;";
        auto customer = sql(database, query);
        if (customer.empty()) throw std::runtime_error("No customer with known default login credentials and accounts in the active database");
        if (service("app") != app || runtime_database(app) != database)
            throw std::runtime_error("Active database changed while reading the customer; try again");
        return {database, customer};
    }
    StatusSummary status() {
        auto requested = active();
        auto app = service("app");
        auto current = app.empty() ? "not running" : runtime_database(app);
        std::cout << "\nApp database NOW: " << database_label(current) << '\n'
                  << "Saved database selection in " << fs::relative(env, infra.parent_path()).string() << ": " << database_label(requested);
        if (current == requested) std::cout << " (applied)\n";
        else std::cout << " (NOT APPLIED to the app)\n";
        bool mismatch = current != requested;
        std::string app_storage = app.empty() ? "" : runtime_storage(app).source;
        bool storage_mismatch = false;
        bool volume_pending = false;
        std::string worker_pdf_location;
        std::cout << "\nRunning service settings:\n";
        for (auto name : {"app", "reporting-worker", "pdf-signer"}) {
            auto ids = services(name);
            if (ids.empty()) {
                std::cout << "  " << name << ": not running\n";
                mismatch = true;
            }
            for (size_t i = 0; i < ids.size(); ++i) {
                auto database = runtime_database(ids[i]);
                mismatch = mismatch || database != requested;
                auto storage = runtime_storage(ids[i]);
                volume_pending = volume_pending || (storage.type == "volume" && storage.name != document_volume(requested));
                auto label = std::string(name) + (ids.size() > 1 ? " #" + std::to_string(i + 1) : "");
                std::cout << "  " << label << ": " << database_label(database);
                if (storage.type == "volume") std::cout << " | PDF volume: " << storage.name;
                else if (storage.type == "bind") std::cout << " | PDF folder: " << storage.source;
                else std::cout << " | PDF mount: not found";
                if (!storage.type.empty()) std::cout << (storage.access == "true" ? " (read/write)" : " (read-only)");
                std::cout << '\n';
                if (std::string(name) == "reporting-worker") {
                    auto location = storage.type == "volume" ? "volume " + storage.name : storage.source;
                    if (location.empty()) location = "not verified";
                    if (worker_pdf_location.empty()) worker_pdf_location = location;
                    else if (worker_pdf_location != location) worker_pdf_location = "multiple locations (see service settings above)";
                    if (!storage.type.empty()) {
                        std::cout << "    PDFs saved inside container: " << storage.destination << '\n'
                                  << "    PDF directory on Docker host: " << storage.source << '\n';
                    } else std::cout << "    PDF write location could not be verified.\n";
                    storage_mismatch = storage_mismatch || (!app.empty() && (storage.type.empty() || app_storage != storage.source));
                }
            }
        }
        if (mismatch) {
            std::cout << "\n! Saved selection and running services are not aligned.\n"
                      << "  Choose 'Switch to synthetic database' or 'Switch back to normal database' to apply that database.\n";
        }
        if (storage_mismatch) std::cout << "! App and worker PDF storage do not match or could not be verified.\n";
        if (volume_pending && !mismatch)
            std::cout << "! Running PDF volume differs from the selected database's volume. Switch to reapply the database and storage settings.\n";
        std::cout << "\nAfter applying the saved selection, the tool will use PDF volume:\n  "
                  << document_volume(requested) << '\n'
                  << "PDFs are stored in Docker storage, outside the repository.\n\nDatabase contents:\n";
        std::cout << "NORMAL DB (nordiska_v2):\n  " << database_contents("nordiska_v2") << '\n';
        if (exists()) std::cout << "SYNTHETIC DB (nordiska_synthetic):\n  " << database_contents("nordiska_synthetic") << '\n';
        else std::cout << "SYNTHETIC DB (nordiska_synthetic): not created\n";
        std::cout << "Documents = registered generated PDFs; files live in the PDF storage shown above.\n";
        return {current, worker_pdf_location.empty() ? "worker not running" : worker_pdf_location, mismatch || volume_pending || storage_mismatch};
    }
};

static int64_t number(const std::string& value, const std::string& option) {
    size_t end = 0;
    auto result = std::stoll(value, &end);
    if (end != value.size() || result < 0) throw std::runtime_error("Invalid " + option + ": " + value);
    return result;
}
static void help() {
    std::cout << R"(Nordiska synthetic database manager — native Linux release
Usage: ./tools/syntheticDB/nordiska-synthetic-linux-x86_64 [options]
  --customers N   Replace synthetic data with N customers (1..37,620,000)
  --seed N        Random seed (default: 42)
  --year N        Reporting year (default: 2025; allowed: 2000..2100)
  --threads N     Generation threads (default: available CPU cores)
  --append        Add customers without replacing existing synthetic data
  --stress-text   Generate long transaction descriptions
  --keep-indexes  Keep secondary ledger indexes during bulk creation (diagnostic)
  --switch DB     nordiska_synthetic or nordiska_v2; recreate app, worker and signer
  --status        Show configured/running databases and actual row counts
  --get-random-customer  Print a known customer login from the active app database
  --menu, --tui   Interactive menu with customer-count presets
  --reset         Replace synthetic database with an empty cloned V2 schema
  --delete        Delete synthetic database (--drop is an alias)
  --delete-documents  Delete synthetic PDFs, document metadata and PDF jobs
  --benchmark     Generate without accessing Docker or writing a database
  --help          Show this help
  --version       Show native release version

With no arguments: interactive menu in a terminal, otherwise status.
Local Compose: infra/docker-compose.yml; secrets/selection: infra/.env (legacy infra/v2/.env supported).
Synthetic PDFs use a separate volume; normal PDFs are preserved.
Personal numbers count from the seed through valid dates 1905-01-01..2007-12-31,
using individual numbers 000..999 and a calculated Luhn checksum.
Everyone is aged 18..120 at the end of 2025. Oversized requests fail before DB changes.

Standard benchmark setup:
  ./tools/syntheticDB/nordiska-synthetic-linux-x86_64 --customers 10000 --seed 42 --year 2025 --threads 4
  ./tools/syntheticDB/nordiska-synthetic-linux-x86_64 --switch nordiska_synthetic
)";
}

struct SeedOptions {
    int64_t customers = 10000, seed = 42, year = 2025;
    int64_t threads = std::max(1u, std::thread::hardware_concurrency());
    bool append = false, stress = false, keep_indexes = false;
};

static Args generation_args(const SeedOptions& options) {
    Args result{"nordiska-synthetic", "--customers", std::to_string(options.customers), "--threads", std::to_string(options.threads),
                "--seed", std::to_string(options.seed), "--year", std::to_string(options.year)};
    if (options.stress) result.push_back("--stress-text");
    if (options.keep_indexes || options.append) result.push_back("--keep-indexes");
    return result;
}

static void generate(Args generation, const nordiska::synthetic::ProgressCallback& progress = {}) {
    std::vector<char*> args;
    for (auto& value : generation) args.push_back(value.data());
    args.push_back(nullptr);
    if (generate_main(static_cast<int>(generation.size()), args.data(), progress) != 0)
        throw std::runtime_error("Native ingestion failed; synthetic data may be partial. Resolve the error and rerun a replacement seed");
}

static void seed_database(DatabaseManager& manager, const SeedOptions& options, bool show_status = true,
                          const nordiska::synthetic::ProgressCallback& progress = {}) {
    auto generation = generation_args(options);
    if (options.append && manager.exists()) {
        if (manager.needs_normal_database()) manager.switch_to("nordiska_v2");
        auto values = manager.sql("nordiska_synthetic", R"sql(SELECT
          COALESCE((SELECT MAX("Id") FROM banking.customers),0) || ' ' ||
          COALESCE((SELECT MAX("Id") FROM banking.savings_accounts),0) || ' ' ||
          COALESCE((SELECT MAX("Id") FROM banking.ledger_entries),0);)sql");
        std::istringstream ids(values);
        for (auto name : {"--start-cust-id", "--start-acct-id", "--start-tx-id"}) {
            int64_t id;
            if (!(ids >> id) || id < 0 || id == std::numeric_limits<int64_t>::max())
                throw std::runtime_error("Invalid existing identity sequence");
            generation.insert(generation.end(), {name, std::to_string(id + 1)});
        }
    } else {
        std::cout << "Replacing nordiska_synthetic with a cloned V2 schema...\n";
        manager.reset();
    }
    auto role = manager.sql("nordiska_synthetic", "SELECT \"Id\" FROM banking.\"AspNetRoles\" WHERE \"NormalizedName\"='CUSTOMER';");
    if (role.empty() || number(role, "Customer role ID") <= 0)
        throw std::runtime_error("Customer role missing; start the development app before seeding");
    generation.insert(generation.end(), {"--container", manager.container, "--db", "nordiska_synthetic", "--customer-role-id", role});
    generate(generation, progress);
    if (show_status) manager.status();
}

static int64_t prompt_customers() {
    using namespace nordiska::synthetic;
    std::string notice;
    while (true) {
        auto input = terminal::screen_input("Create synthetic data", " Custom amount: ", "Enter Continue   Esc Back", notice);
        try {
            auto result = number(input, "customer count");
            if (result >= 1 && result <= static_cast<int64_t>(nordiska::synthetic::personal_number_capacity)) return result;
        } catch (const std::exception&) {}
        notice = "Enter a whole number from 1 to 37,620,000.";
    }
}

static int64_t select_customer_count(size_t& selected) {
    using namespace nordiska::synthetic;
    const std::vector<std::string> header{" Create synthetic data", " --------------------------------------------------"};
    const std::vector<terminal::Option> options{{"100"}, {"1000"}, {"10k"}, {"100k"}, {"1m"}, {"10m"},
        {"MAX (" + format_count(std::to_string(personal_number_capacity)) + ")"}, {"Custom amount"}};
    const std::array<int64_t, 7> counts{100, 1000, 10000, 100000, 1000000, 10000000,
        static_cast<int64_t>(personal_number_capacity)};
    while (true) {
        selected = terminal::screen_menu(header, options, selected, "", "Back");
        if (selected < counts.size()) return counts[selected];
        try { return prompt_customers(); }
        catch (const terminal::Cancelled& cancel) { if (cancel.eof) throw; }
    }
}

static int64_t edit_seed(int64_t seed) {
    using namespace nordiska::synthetic;
    std::string notice;
    while (true) {
        const auto input = terminal::screen_input("Generation seed", " Seed [" + std::to_string(seed) + "]: ",
                                                  "Enter Save   Esc Back", notice);
        if (input.empty()) return seed;
        try { return number(input, "seed"); } catch (const std::exception&) {}
        notice = "Enter a whole number from 0 to 9,223,372,036,854,775,807.";
    }
}

static void configure_creation(SeedOptions& options, size_t& customer_choice) {
    using namespace nordiska::synthetic;
    while (true) {
        options.customers = select_customer_count(customer_choice);
        size_t selected = 1;
        while (true) {
            try {
                const std::vector<std::string> header{" Create synthetic data", " --------------------------------------------------",
                    " Customers: " + format_count(std::to_string(options.customers)), ""};
                selected = terminal::screen_menu(header, {{"Seed: " + std::to_string(options.seed)}, {"Start creation"}}, selected, "", "Back");
            } catch (const terminal::Cancelled& cancel) { if (cancel.eof) throw; break; }
            if (selected == 1) return;
            try { options.seed = edit_seed(options.seed); }
            catch (const terminal::Cancelled& cancel) { if (cancel.eof) throw; }
        }
    }
}

static std::string duration_text(double seconds) {
    const auto value = int64_t(std::ceil(std::max(0.0, seconds)));
    std::ostringstream text;
    if (value >= 3600) text << value / 3600 << ':';
    text << std::setfill('0') << std::setw(2) << (value / 60) % 60 << ':' << std::setw(2) << value % 60;
    return text.str();
}

static void create_with_progress(DatabaseManager& manager, const SeedOptions& options) {
    using namespace nordiska::synthetic;
    std::mutex mutex;
    GenerationProgress progress;
    const auto started = std::chrono::steady_clock::now();
    const auto report = [&](const GenerationProgress& value) { std::lock_guard guard(mutex); progress = value; };
    const auto details = [&]() -> std::vector<std::string> {
        GenerationProgress current;
        { std::lock_guard guard(mutex); current = progress; }
        const auto elapsed = std::chrono::duration<double>(std::chrono::steady_clock::now() - started).count();
        std::string bar = "Waiting for database...";
        if (current.fraction >= 0) {
            const auto fraction = std::clamp(current.fraction, 0.0, 1.0);
            const auto filled = int(fraction * 16);
            bar = "[" + std::string(filled, '#') + std::string(16 - filled, '-') + "] " +
                  std::to_string(int(fraction * 100)) + "% | Customers ready: " +
                  format_count(std::to_string(current.total > 0 ? current.completed : current.customers)) +
                  " / " + format_count(std::to_string(current.total > 0 ? current.total : options.customers));
        }
        const auto rate = format_count(std::to_string(int64_t(std::max(0.0, current.customers_per_second))));
        const auto rows = "Transactions saved: " + format_count(std::to_string(current.transactions)) +
                          " | Rate: " + rate + " customers/s";
        const auto eta = current.eta_seconds >= 0 ? "~" + duration_text(current.eta_seconds)
            : current.completed == options.customers ? "finishing database..." : "calibrating " + format_count(std::to_string(std::min<int64_t>(500, options.customers))) + " customers...";
        return {"Customers: " + format_count(std::to_string(options.customers)) + " | Seed: " + std::to_string(options.seed),
                "Stage: " + current.stage, bar, rows,
                "Generated: " + format_count(std::to_string(current.generated_customers)) +
                " | Transferred: " + format_count(std::to_string(current.transferred_customers)), "Elapsed: " + duration_text(elapsed) + " | Remaining: " + eta};
    };
    terminal::loading("Creating synthetic data", [&] {
        if (manager.exists()) throw std::runtime_error("Synthetic DB already exists; delete it before creating a new one");
        seed_database(manager, options, false, report);
    }, details);
}

enum class MenuAction { Switch, Create, Delete, Documents, Login, Retry, Exit };

static std::string environment_name(const std::string& database) {
    if (database == "nordiska_v2") return "NORMAL";
    if (database == "nordiska_synthetic") return "SYNTHETIC";
    if (database == "offline") return "OFFLINE";
    return "UNKNOWN";
}

static std::vector<std::string> main_screen(const DatabaseManager::MenuState& state) {
    auto active = environment_name(state.database);
    std::vector<std::string> rows{
        " Synthetic Data Manager [Active DB: " + active + "]",
        " --------------------------------------------------"
    };
    std::ostringstream columns;
    columns << std::setw(18) << "" << std::setw(12) << "NORMAL" << std::setw(14) << "SYNTHETIC";
    rows.push_back(columns.str());
    const std::array<std::string, 5> labels{"Customers", "Accounts", "Transactions", "Documents", "Size"};
    for (size_t i = 0; i < labels.size(); ++i) {
        auto normal = state.normal[i], synthetic = state.synthetic[i];
        if (i < 4) {
            if (normal != "-") normal = format_count(normal);
            if (synthetic != "-") synthetic = format_count(synthetic);
        }
        std::ostringstream row;
        row << ' ' << std::left << std::setw(17) << labels[i] << std::right << std::setw(12) << normal << std::setw(14) << synthetic;
        rows.push_back(row.str());
    }
    rows.push_back(" --------------------------------------------------");
    return rows;
}

static int run_menu(DatabaseManager& manager) {
    using namespace nordiska::synthetic;
    terminal::Screen screen;
    MenuAction selected = MenuAction::Switch;
    size_t customer_choice = 2;
    int64_t generation_seed = 42;
    std::string notice;
    try {
        while (true) {
            DatabaseManager::MenuState state;
            bool loaded = true;
            try { terminal::loading("Reading database state", [&] { state = manager.menu_state(); }); }
            catch (const terminal::OperationFailure&) {
                loaded = false;
                notice = "Error: could not read database state. Check Docker/PostgreSQL and retry.";
            }
            std::vector<MenuAction> actions;
            std::vector<terminal::Option> options;
            auto add = [&](MenuAction action, const std::string& label, bool gap = false, bool enabled = true) {
                actions.push_back(action);
                options.push_back({label, gap, enabled});
            };
            std::string target = state.database == "nordiska_synthetic" ? "nordiska_v2" : "nordiska_synthetic";
            if (!valid_database(state.database)) target = "nordiska_v2";
            if (!state.app_running) target = state.start_database;
            if (loaded) {
                if (!state.app_running)
                    add(MenuAction::Switch, "Start app on " + environment_name(target) + " DB");
                else if (state.synthetic_exists || state.database == "nordiska_synthetic")
                    add(MenuAction::Switch, target == "nordiska_v2"
                        ? (state.database == "nordiska_synthetic" ? "Switch back to NORMAL DB" : "Switch to NORMAL DB")
                        : "Switch to SYNTHETIC DB");
                if (state.synthetic_exists)
                    add(MenuAction::Delete, "Delete synthetic DB and return to NORMAL", !actions.empty());
                else
                    add(MenuAction::Create, "Create synthetic data", !actions.empty());
                const auto document_label = !state.documents_available ? " (no documents)" :
                    !state.synthetic_exists || state.synthetic[3] == "0" ? " (stored files)" : "";
                add(MenuAction::Documents, "Delete synthetic documents" + std::string(document_label), false, state.documents_available);
                if (state.login_available) add(MenuAction::Login, "Show random customer login");
            } else add(MenuAction::Retry, "Retry loading database state");
            add(MenuAction::Exit, "Exit");
            auto position = std::find(actions.begin(), actions.end(), selected);
            auto index = position == actions.end() ? size_t{0} : size_t(position - actions.begin());
            selected = actions[terminal::screen_menu(main_screen(state), options, index, notice)];
            notice.clear();
            if (selected == MenuAction::Exit) return 0;
            if (selected == MenuAction::Retry) continue;
            try {
                if (selected == MenuAction::Switch) {
                    const auto label = state.app_running
                        ? "Switching DB: " + environment_name(state.database) + " -> " + environment_name(target)
                        : "Starting app on " + environment_name(target) + " DB";
                    terminal::loading(label,
                        [&] { manager.switch_to(target); });
                    notice = state.app_running ? "OK: switched to " + environment_name(target) + "."
                                               : "OK: app started on " + environment_name(target) + ".";
                } else if (selected == MenuAction::Create) {
                    SeedOptions parameters;
                    parameters.seed = generation_seed;
                    configure_creation(parameters, customer_choice);
                    generation_seed = parameters.seed;
                    create_with_progress(manager, parameters);
                    notice = "OK: created " + format_count(std::to_string(parameters.customers)) + " synthetic customers (seed " +
                             std::to_string(parameters.seed) + ").";
                } else if (selected == MenuAction::Delete) {
                    if (!terminal::confirm("Confirm deletion", " Delete synthetic DB and return to NORMAL?")) {
                        notice = "Cancelled.";
                        continue;
                    }
                    terminal::loading("Deleting synthetic DB and returning to NORMAL", [&] { manager.drop(); });
                    notice = "OK: synthetic DB deleted; NORMAL selected.";
                } else if (selected == MenuAction::Login) {
                    DatabaseManager::CustomerLogin login;
                    terminal::loading("Reading customer login", [&] { login = manager.get_random_customer(); });
                    terminal::page(login.title(), login.details);
                } else if (selected == MenuAction::Documents) {
                    if (!terminal::confirm("Delete synthetic documents", " Delete all synthetic PDFs and PDF jobs?")) {
                        notice = "Cancelled.";
                        continue;
                    }
                    terminal::loading("Deleting synthetic documents", [&] { manager.delete_documents(); });
                    notice = "OK: synthetic PDFs and PDF jobs deleted.";
                }
            } catch (const terminal::Cancelled& cancel) {
                if (cancel.eof) throw;
                notice = "Cancelled.";
            } catch (const terminal::OperationFailure& error) {
                notice = "Error: " + std::string(error.what());
            } catch (const std::exception& error) {
                notice = "Error: " + std::string(error.what());
            }
        }
    } catch (const terminal::Cancelled&) {
        if (!terminal::interactive()) std::cout << "Cancelled.\n";
        return 0;
    }
}
int main(int argc, char** argv) {
    if (!std::setlocale(LC_CTYPE, "C.UTF-8")) std::setlocale(LC_CTYPE, "");
    signal(SIGPIPE, SIG_IGN);
    try {
        int64_t customers = 10000, seed = 42, year = 2025;
        int64_t threads = std::max(1u, std::thread::hardware_concurrency());
        bool seed_requested = false, status = false, reset = false, drop = false, append = false, stress = false, benchmark = false, menu = false, random_customer = false, delete_documents = false, keep_indexes = false;
        std::string target;
        for (int i = 1; i < argc; ++i) {
            std::string option = argv[i];
            if (option == "--help" || option == "-h") { help(); return 0; }
            if (option == "--version") { std::cout << "nordiska-synthetic 1.3.0 linux-x86_64\n"; return 0; }
            if (option == "--status") status = true;
            else if (option == "--get-random-customer") random_customer = true;
            else if (option == "--menu" || option == "--tui") menu = true;
            else if (option == "--reset") reset = true;
            else if (option == "--delete" || option == "--drop") drop = true;
            else if (option == "--delete-documents") delete_documents = true;
            else if (option == "--append") append = true;
            else if (option == "--stress-text") stress = true;
            else if (option == "--keep-indexes") keep_indexes = true;
            else if (option == "--benchmark") benchmark = true;
            else if (option == "--customers" || option == "--threads" || option == "--seed" || option == "--year" || option == "--switch") {
                if (++i >= argc) throw std::runtime_error("Missing value for " + option);
                if (option == "--switch") target = argv[i];
                else {
                    auto value = number(argv[i], option);
                    if (option == "--customers") { customers = value; seed_requested = true; }
                    if (option == "--threads") threads = value;
                    if (option == "--seed") seed = value;
                    if (option == "--year") year = value;
                }
            } else throw std::runtime_error("Unknown option: " + option + "; use --help");
        }
        // Validate everything before any database or configuration mutation.
        if (customers <= 0 || threads <= 0 || threads > std::numeric_limits<int>::max() || year < 2000 || year > 2100)
            throw std::runtime_error("Customer/thread counts must be positive; year must be 2000..2100");
        if (customers > static_cast<int64_t>(nordiska::synthetic::personal_number_capacity))
            throw std::runtime_error("Personal-number capacity exhausted: maximum 37,620,000 customers; no database changes made");
        if (!target.empty() && !valid_database(target)) throw std::runtime_error("Unsupported --switch database");
        if (append && !seed_requested) throw std::runtime_error("--append requires --customers");
        if ((reset || drop) && (seed_requested || !target.empty() || reset == drop))
            throw std::runtime_error("Use --reset or --delete separately from seeding and switching");
        if (benchmark && (append || reset || drop || status || !target.empty()))
            throw std::runtime_error("--benchmark cannot be combined with database-management actions");

        if (menu && argc != 2) throw std::runtime_error("Use --menu separately from command-line actions");
        if (random_customer && (argc != (status ? 3 : 2) || seed_requested || reset || drop || benchmark || !target.empty() || menu))
            throw std::runtime_error("Use --get-random-customer on its own or with --status");
        if (delete_documents && (argc != (status ? 3 : 2) || seed_requested || reset || drop || benchmark || random_customer || !target.empty() || menu))
            throw std::runtime_error("Use --delete-documents on its own or with --status");
        if (keep_indexes && !seed_requested && !benchmark) throw std::runtime_error("--keep-indexes requires --customers or --benchmark");
        SeedOptions options{customers, seed, year, threads, append, stress, keep_indexes};
        if (benchmark) {
            auto generation = generation_args(options);
            generation.push_back("--benchmark");
            generate(generation);
            return 0;
        }
        DatabaseManager manager;
        if (menu || (argc == 1 && isatty(STDIN_FILENO) && isatty(STDOUT_FILENO))) return run_menu(manager);
        if (reset) { manager.reset(); std::cout << "Synthetic database reset\n"; }
        if (drop) { manager.drop(); std::cout << "Synthetic database deleted; its PDF volume was retained\n"; }
        if (delete_documents) { manager.delete_documents(); std::cout << "Synthetic PDFs, document metadata and PDF jobs deleted\n"; }
        if (seed_requested) seed_database(manager, options);
        if (!target.empty()) manager.switch_to(target);
        if (random_customer) {
            auto login = manager.get_random_customer();
            std::cout << login.title() << "\n --------------------------------------------------\n" << login.details << '\n';
        }
        if (status || (!seed_requested && !reset && !drop && !delete_documents && target.empty() && !random_customer)) manager.status();
        return 0;
    } catch (const std::exception& error) {
        std::cerr << "Error: " << error.what() << '\n';
        return 1;
    }
}
