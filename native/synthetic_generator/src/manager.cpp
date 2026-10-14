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
#include <sys/wait.h>
#include <unistd.h>

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
        if (fs::exists(current / "infra/v2/docker-compose.yml")) return current;
        auto parent = current.parent_path();
        if (parent == current) break;
        current = parent;
    }
    throw std::runtime_error("Run the checked-in executable inside the repository checkout");
}
static bool valid_database(const std::string& name) {
    return name == "nordiska_v2" || name == "nordiska_synthetic";
}

class DatabaseManager {
    fs::path infra, env;
    std::vector<fs::path> files;
    static inline const std::regex selection{R"(^[ \t]*NORDISKA_DATABASE[ \t]*=[ \t]*(.*)$)"};
    std::string service(const std::string& name) {
        std::istringstream rows(compose({"ps", "--quiet", name}));
        std::string first, extra;
        rows >> first;
        if (rows >> extra) throw std::runtime_error("Expected one " + name + " container in nordiska-v2");
        return first;
    }
    void recreate() {
        compose({"up", "-d", "--no-deps", "--force-recreate", "--wait", "--wait-timeout", "180",
                 "api", "reporting-worker", "pdf-signer"});
    }
public:
    std::string container;
    DatabaseManager() : infra(repo_root() / "infra/v2"), env(infra / ".env"),
                        files{infra / "docker-compose.yml", infra / "docker-compose.override.yml"} {
        if (!fs::is_regular_file(env)) throw std::runtime_error("infra/v2/.env is missing; complete Docker guide step 1 first");
        container = service("db");
        if (container.empty()) throw std::runtime_error("The nordiska-v2 db service is not running; run DevSetup start-stack first");
        auto api = service("api");
        if (!api.empty()) {
            auto labels = run({"docker", "inspect", "--format",
                "{{index .Config.Labels \"com.docker.compose.project.config_files\"}}", api});
            std::istringstream paths(labels);
            std::string path;
            while (std::getline(paths, path, ','))
                if (fs::path(trim(path)).lexically_normal() == infra / "docker-compose.frontend-local.yml")
                    files = {infra / "docker-compose.yml", infra / "docker-compose.frontend-local.yml"};
        }
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
        bool found = false;
        while (std::getline(lines, line)) {
            if (std::regex_match(line, selection)) { line = "NORDISKA_DATABASE=" + database; found = true; }
            output += line + '\n';
        }
        if (!found) output = "NORDISKA_DATABASE=" + database + '\n' + output;
        TempFile replacement(infra / ".env.synthetic.XXXXXX");
        replacement.write(output);
        fs::permissions(replacement.path, fs::status(env).permissions());
        fs::rename(replacement.path, env);
    }
    std::string compose(const Args& tail) const {
        // Shell exports must not override the selection saved in .env.
        if (setenv("NORDISKA_DATABASE", active().c_str(), 1) != 0)
            throw std::runtime_error("Cannot set database selection for Compose");
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
    bool exists() const {
        return sql("postgres", "SELECT 1 FROM pg_database WHERE datname = 'nordiska_synthetic';") == "1";
    }
    void switch_to(const std::string& target) {
        if (!valid_database(target)) throw std::runtime_error("Unsupported database: " + target);
        if (target == "nordiska_synthetic" && !exists())
            throw std::runtime_error("nordiska_synthetic does not exist; seed it first");
        auto previous = active();
        select(target);
        try { recreate(); }
        catch (const std::exception&) {
            select(previous);
            try { recreate(); }
            catch (const std::exception&) {
                throw std::runtime_error("Switch and rollback failed; previous .env selection restored. Recreate the V2 services before continuing");
            }
            throw std::runtime_error("Database switch failed; previous services restored");
        }
        std::cout << "V2 API, worker and signer now use " << target << '\n';
    }
    void drop() {
        if (active() == "nordiska_synthetic") switch_to("nordiska_v2");
        sql("postgres", "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = 'nordiska_synthetic';");
        sql("postgres", "DROP DATABASE IF EXISTS nordiska_synthetic;");
    }
    void restore(const Args& options) {
        Args args{"docker", "exec", container, "pg_dump", "-U", "nordiska_bootstrap"};
        args.insert(args.end(), options.begin(), options.end());
        args.push_back("nordiska_v2");
        auto dump = run(args); // Do not restore an incomplete dump if pg_dump fails.
        run({"docker", "exec", "-i", container, "psql", "-U", "nordiska_bootstrap", "-d", "nordiska_synthetic",
             "-v", "ON_ERROR_STOP=1", "-q"}, &dump);
    }
    void reset() {
        drop();
        sql("postgres", "CREATE DATABASE nordiska_synthetic OWNER nordiska_bootstrap;");
        restore({"--schema-only"});
        restore({"--data-only", "-t", "banking.account_type_configs", "-t", "banking.\"AspNetRoles\"", "-t", "faq.faq_entries"});
        sql("postgres", "GRANT CONNECT ON DATABASE nordiska_synthetic TO nordiska_migrator, nordiska_api, nordiska_reporting_worker;");
        run({"docker", "exec", container, "psql", "-U", "nordiska_bootstrap", "-d", "nordiska_synthetic",
             "-v", "ON_ERROR_STOP=1", "-f", "/opt/nordiska/post-migration-permissions.sql"});
    }
    void status() const {
        std::cout << "Configured V2 database: " << active() << '\n';
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
  --switch DB     nordiska_synthetic or nordiska_v2; recreate API, worker and signer
  --status        Show selection and actual database counts (default action)
  --reset         Replace synthetic database with an empty cloned V2 schema
  --delete        Delete synthetic database (--drop is an alias)
  --benchmark     Generate without accessing Docker or writing a database
  --help          Show this help
  --version       Show native release version

Standard benchmark setup:
  ./synthetic-data-manager --customers 10000 --seed 42 --year 2025 --threads 4
  ./synthetic-data-manager --switch nordiska_synthetic
)";
}
int main(int argc, char** argv) {
    signal(SIGPIPE, SIG_IGN);
    try {
        int64_t customers = 10000, seed = 42, year = 2025;
        int64_t threads = std::max(1u, std::thread::hardware_concurrency());
        bool seed_requested = false, status = false, reset = false, drop = false, append = false, stress = false, benchmark = false;
        std::string target;
        for (int i = 1; i < argc; ++i) {
            std::string option = argv[i];
            if (option == "--help" || option == "-h") { help(); return 0; }
            if (option == "--version") { std::cout << "nordiska-synthetic 1.0.0 linux-x86_64\n"; return 0; }
            if (option == "--status") status = true;
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

        Args generation{"nordiska-synthetic", "--customers", std::to_string(customers), "--threads", std::to_string(threads),
                        "--seed", std::to_string(seed), "--year", std::to_string(year)};
        if (stress) generation.push_back("--stress-text");
        auto generate = [&]() {
            std::vector<char*> args;
            for (auto& value : generation) args.push_back(value.data());
            args.push_back(nullptr);
            if (generate_main(static_cast<int>(generation.size()), args.data()) != 0)
                throw std::runtime_error("Native ingestion failed; synthetic data may be partial. Resolve the error and rerun a replacement seed");
        };
        if (benchmark) { generation.push_back("--benchmark"); generate(); return 0; }
        DatabaseManager manager;
        if (reset) { manager.reset(); std::cout << "Synthetic database reset\n"; }
        if (drop) { manager.drop(); std::cout << "Synthetic database deleted\n"; }
        if (seed_requested) {
            if (append && manager.exists()) {
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
                throw std::runtime_error("Customer role missing; start the development API before seeding");
            generation.insert(generation.end(), {"--container", manager.container, "--db", "nordiska_synthetic", "--customer-role-id", role});
            generate();
            manager.status();
        }
        if (!target.empty()) manager.switch_to(target);
        if (status || (!seed_requested && !reset && !drop && target.empty())) manager.status();
        return 0;
    } catch (const std::exception& error) {
        std::cerr << "Error: " << error.what() << '\n';
        return 1;
    }
}
