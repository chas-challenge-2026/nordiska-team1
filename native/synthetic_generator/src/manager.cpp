#include <algorithm>
#include <cerrno>
#include <csignal>
#include <cstdio>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <limits>
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

int generate_main(int argc, char** argv);
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
struct TempFile {
    fs::path path;
    explicit TempFile(fs::path pattern = fs::temp_directory_path() / "nordiska-synthetic.XXXXXX") {
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
    // Resolve the real executable, including root compatibility symlinks.
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
static fs::path local_env(const fs::path& infra) {
    auto env = infra / "v2/.env";
    if (!fs::is_regular_file(env)) throw std::runtime_error("infra/v2/.env is missing; run DevSetup first");
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
    static inline const std::regex selection{R"(^[ \t]*NORDISKA_DATABASE[ \t]*=[ \t]*(.*)$)"};
    std::vector<std::string> services(const std::string& name) {
        std::istringstream rows(compose({"ps", "--quiet", name}));
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
    void write_env(const std::string& content) {
        TempFile replacement(env.parent_path() / ".env.synthetic.XXXXXX");
        replacement.write(content);
        fs::permissions(replacement.path, fs::status(env).permissions());
        fs::rename(replacement.path, env);
    }
    void recreate() {
        compose({"up", "-d", "--no-deps", "--force-recreate", "--wait", "--wait-timeout", "180",
                 "--scale", "reporting-worker=" + std::to_string(worker_count),
                 "app", "reporting-worker", "pdf-signer"});
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
    std::string container;
    DatabaseManager() : infra(repo_root() / "infra"), env(local_env(infra)),
                        lock(env.parent_path() / ".synthetic-data.lock"),
                        files{infra / "docker-compose.yml", infra / "docker-compose.override.yml"} {
        container = service("db");
        if (container.empty()) throw std::runtime_error("The nordiska-v2 db service is not running; run DevSetup start-stack first");
        auto app = service("app");
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
        select(target);
        try { recreate(); }
        catch (const std::exception&) {
            write_env(previous_env);
            try { recreate(); }
            catch (const std::exception&) {
                throw std::runtime_error("Switch and rollback failed; previous .env selection restored. Recreate the V2 services before continuing");
            }
            throw std::runtime_error("Database switch failed; previous services restored");
        }
        std::cout << "App, worker and signer now use " << target << '\n'
                  << "PDF volume: " << document_volume(target) << '\n';
    }
    void drop() {
        if (active() == "nordiska_synthetic") switch_to("nordiska_v2");
        sql("postgres", "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = 'nordiska_synthetic';");
        sql("postgres", "DROP DATABASE IF EXISTS nordiska_synthetic;");
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
    void status() {
        std::cout << "Configured database: " << active() << '\n'
                  << "PDF volume selected by this tool: " << document_volume(active()) << '\n';
        for (auto name : {"app", "reporting-worker", "pdf-signer"}) {
            auto ids = services(name);
            if (ids.empty()) std::cout << name << ": not running\n";
            for (const auto& id : ids) std::cout << name << ": " << runtime_database(id) << '\n';
        }
        const std::string query = R"sql(SELECT (SELECT COUNT(*) FROM banking.customers) || ' customers | ' ||
          (SELECT COUNT(*) FROM banking.savings_accounts) || ' accounts | ' ||
          (SELECT COUNT(*) FROM banking.ledger_entries) || ' ledger entries | ' || pg_size_pretty(pg_database_size(current_database()));)sql";
        std::cout << "nordiska_v2: " << sql("nordiska_v2", query) << '\n';
        if (exists()) std::cout << "nordiska_synthetic: " << sql("nordiska_synthetic", query) << '\n';
        else std::cout << "nordiska_synthetic: not created\n";
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
Usage: ./synthetic-data-manager [options]
  --customers N   Replace synthetic data with N customers (explicit seed action)
  --seed N        Random seed (default: 42)
  --year N        Reporting year (default: 2025; allowed: 2000..2100)
  --threads N     Generation threads (default: available CPU cores)
  --append        Add customers without replacing existing synthetic data
  --stress-text   Generate long transaction descriptions
  --switch DB     nordiska_synthetic or nordiska_v2; recreate app, worker and signer
  --status        Show configured/running databases and actual row counts
  --menu, --tui   Interactive menu with workload presets and CPU selection
  --reset         Replace synthetic database with an empty cloned V2 schema
  --delete        Delete synthetic database (--drop is an alias)
  --benchmark     Generate without accessing Docker or writing a database
  --help          Show this help
  --version       Show native release version

With no arguments: interactive menu in a terminal, otherwise status.
Local Compose: infra/docker-compose.yml; secrets/selection: infra/v2/.env.
Synthetic PDFs use a separate volume; normal PDFs are preserved.

Standard benchmark setup:
  ./synthetic-data-manager --customers 10000 --seed 42 --year 2025 --threads 4
  ./synthetic-data-manager --switch nordiska_synthetic
)";
}

struct SeedOptions {
    int64_t customers = 10000, seed = 42, year = 2025;
    int64_t threads = std::max(1u, std::thread::hardware_concurrency());
    bool append = false, stress = false;
};

static Args generation_args(const SeedOptions& options) {
    Args result{"nordiska-synthetic", "--customers", std::to_string(options.customers), "--threads", std::to_string(options.threads),
                "--seed", std::to_string(options.seed), "--year", std::to_string(options.year)};
    if (options.stress) result.push_back("--stress-text");
    return result;
}

static void generate(Args generation) {
    std::vector<char*> args;
    for (auto& value : generation) args.push_back(value.data());
    args.push_back(nullptr);
    if (generate_main(static_cast<int>(generation.size()), args.data()) != 0)
        throw std::runtime_error("Native ingestion failed; synthetic data may be partial. Resolve the error and rerun a replacement seed");
}

static void seed_database(DatabaseManager& manager, const SeedOptions& options) {
    auto generation = generation_args(options);
    if (options.append && manager.exists()) {
        if (manager.active() == "nordiska_synthetic") manager.switch_to("nordiska_v2");
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
    generate(generation);
    manager.status();
}

static int64_t prompt_number(const std::string& label, int64_t value, int64_t minimum, int64_t maximum) {
    using namespace nordiska::synthetic;
    while (true) {
        auto input = terminal::line(label + " [" + std::to_string(value) + "]: ");
        if (input.empty()) return value;
        try {
            auto result = number(input, label);
            if (result >= minimum && result <= maximum) return result;
        } catch (const std::exception&) {}
        std::cout << "Enter a number from " << minimum << " to " << maximum << ".\n";
    }
}

static int run_menu(DatabaseManager& manager) {
    using namespace nordiska::synthetic;
    try {
        manager.status();
        while (true) {
            std::cout << "\nNordiska synthetic data manager — selected: " << manager.active() << '\n';
            auto action = terminal::menu("Action", {
                "Create or replace synthetic data", "Append customers", "Switch to synthetic database",
                "Switch back to normal database", "Show database status", "Reset synthetic database to empty schema",
                "Delete synthetic database", "Exit"}, 7);
            if (action == 7) return 0;
            try {
                if (action <= 1) {
                    SeedOptions options;
                    options.append = action == 1;
                    const std::vector<int64_t> presets{50, 1000, 10000, 50000, 100000, 1000000};
                    auto choice = terminal::menu("Customers", {"50 (smoke test)", "1,000", "10,000 (standard benchmark)",
                        "50,000", "100,000", "1,000,000", "Custom count"}, 2);
                    options.customers = choice < presets.size() ? presets[choice] : prompt_number("Customers", 10000, 1, 10000000);
                    options.threads = prompt_number("CPU threads", options.threads, 1, std::numeric_limits<int>::max());
                    options.year = prompt_number("Reporting year", options.year, 2000, 2100);
                    options.seed = prompt_number("Random seed", options.seed, 0, std::numeric_limits<int64_t>::max());
                    options.stress = terminal::confirm("Use long transaction descriptions?");
                    std::cout << "\nTarget: nordiska_synthetic | Customers: " << options.customers << " | Threads: " << options.threads
                              << " | Year: " << options.year << " | Seed: " << options.seed
                              << " | Mode: " << (options.append ? "append" : "replace") << '\n';
                    if (terminal::confirm("Generate this workload?")) {
                        seed_database(manager, options);
                        if (terminal::confirm("Switch the app to the synthetic database now?")) manager.switch_to("nordiska_synthetic");
                    }
                } else if (action == 2) manager.switch_to("nordiska_synthetic");
                else if (action == 3) manager.switch_to("nordiska_v2");
                else if (action == 4) manager.status();
                else if (action == 5 && terminal::confirm("Replace all synthetic records with an empty schema?")) manager.reset();
                else if (action == 6 && terminal::confirm("Delete nordiska_synthetic? Its PDF volume will be retained.")) manager.drop();
            } catch (const std::exception& error) {
                std::cerr << "Error: " << error.what() << '\n';
            }
            terminal::line("Press Enter to return to the menu...");
        }
    } catch (const terminal::Cancelled&) {
        std::cout << "\nCancelled.\n";
        return 0;
    }
}
int main(int argc, char** argv) {
    signal(SIGPIPE, SIG_IGN);
    try {
        int64_t customers = 10000, seed = 42, year = 2025;
        int64_t threads = std::max(1u, std::thread::hardware_concurrency());
        bool seed_requested = false, status = false, reset = false, drop = false, append = false, stress = false, benchmark = false, menu = false;
        std::string target;
        for (int i = 1; i < argc; ++i) {
            std::string option = argv[i];
            if (option == "--help" || option == "-h") { help(); return 0; }
            if (option == "--version") { std::cout << "nordiska-synthetic 1.1.0 linux-x86_64\n"; return 0; }
            if (option == "--status") status = true;
            else if (option == "--menu" || option == "--tui") menu = true;
            else if (option == "--reset") reset = true;
            else if (option == "--delete" || option == "--drop") drop = true;
            else if (option == "--append") append = true;
            else if (option == "--stress-text") stress = true;
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
        if (!target.empty() && !valid_database(target)) throw std::runtime_error("Unsupported --switch database");
        if (append && !seed_requested) throw std::runtime_error("--append requires --customers");
        if ((reset || drop) && (seed_requested || !target.empty() || reset == drop))
            throw std::runtime_error("Use --reset or --delete separately from seeding and switching");
        if (benchmark && (append || reset || drop || status || !target.empty()))
            throw std::runtime_error("--benchmark cannot be combined with database-management actions");

        if (menu && argc != 2) throw std::runtime_error("Use --menu separately from command-line actions");
        SeedOptions options{customers, seed, year, threads, append, stress};
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
        if (seed_requested) seed_database(manager, options);
        if (!target.empty()) manager.switch_to(target);
        if (status || (!seed_requested && !reset && !drop && target.empty())) manager.status();
        return 0;
    } catch (const std::exception& error) {
        std::cerr << "Error: " << error.what() << '\n';
        return 1;
    }
}
