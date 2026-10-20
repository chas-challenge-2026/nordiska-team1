#include "env_file.hpp"
#include "secret_generator.hpp"
#include "menu.hpp"

#include <iostream>
#include <string>
#include <unistd.h>

using namespace nordiska::env;

static void print_usage(const char* prog) {
    std::cout << "Usage: " << prog << " [options]\n\n"
              << "Interactive Menu (Default when run in a terminal):\n"
              << "  " << prog << "              Open interactive environment manager menu\n"
              << "  " << prog << " --menu       Force open menu\n\n"
              << "CLI Options:\n"
              << "  --db <name>          Set target database (e.g. 'nordiska_v2' or 'nordiska_synthetic')\n"
              << "  --set <KEY>=<VAL>    Set or add an environment variable and save\n"
              << "  --get <KEY>          Print the value of an environment variable\n"
              << "  --list               Print all variables in infra/.env\n"
              << "  --export             Print shell export commands (for: eval $(" << prog << " --export))\n"
              << "  --generate-secrets   Fill missing/empty secrets with secure random values\n"
              << "  --status             Print current database and status overview\n"
              << "  -h, --help           Show this help message\n\n"
              << "Shell Integration:\n"
              << "  source tools/env/set-env.sh   # Run menu and export variables into your current shell\n";
}

int main(int argc, char* argv[]) {
    EnvFile env;
    if (!env.load()) {
        std::cerr << "Warning: Could not load existing .env file. Starting with default/empty configuration.\n";
    }

    if (argc > 1) {
        std::string arg = argv[1];

        if (arg == "-h" || arg == "--help") {
            print_usage(argv[0]);
            return 0;
        }

        if (arg == "--menu" || arg == "-m") {
            TerminalMenu menu(env);
            return menu.run();
        }

        if (arg == "--db") {
            if (argc < 3) {
                std::cerr << "Error: --db requires database name (e.g. nordiska_v2 or nordiska_synthetic)\n";
                return 1;
            }
            std::string db_name = argv[2];
            env.set_database(db_name);
            if (!env.save()) {
                std::cerr << "Error: Failed to save changes to " << env.env_path() << "\n";
                return 1;
            }
            std::cout << "Successfully set NORDISKA_DATABASE=" << db_name << " in " << env.env_path() << "\n";
            return 0;
        }

        if (arg == "--set") {
            if (argc < 3) {
                std::cerr << "Error: --set requires KEY=VALUE\n";
                return 1;
            }
            std::string pair = argv[2];
            auto eq = pair.find('=');
            if (eq == std::string::npos) {
                std::cerr << "Error: Invalid format '" << pair << "'. Expected KEY=VALUE\n";
                return 1;
            }
            std::string key = pair.substr(0, eq);
            std::string val = pair.substr(eq + 1);
            if (key == "NORDISKA_DATABASE") {
                env.set_database(val);
            } else {
                env.set(key, val);
            }
            if (!env.save()) {
                std::cerr << "Error: Failed to save to " << env.env_path() << "\n";
                return 1;
            }
            std::cout << "Successfully set " << key << "=" << val << " in " << env.env_path() << "\n";
            return 0;
        }

        if (arg == "--get") {
            if (argc < 3) {
                std::cerr << "Error: --get requires KEY\n";
                return 1;
            }
            std::string key = argv[2];
            auto val = env.get(key);
            if (val.has_value()) {
                std::cout << *val << "\n";
                return 0;
            } else {
                std::cerr << "Variable '" << key << "' not found.\n";
                return 1;
            }
        }

        if (arg == "--list") {
            for (const auto& [k, v] : env.all_vars()) {
                std::cout << k << "=" << v << "\n";
            }
            return 0;
        }

        if (arg == "--export") {
            std::cout << env.generate_export_commands();
            return 0;
        }

        if (arg == "--generate-secrets") {
            size_t n = SecretGenerator::fill_secrets(env, false);
            if (n > 0) {
                env.save();
                std::cout << "Generated " << n << " missing secret(s) and saved to " << env.env_path() << "\n";
            } else {
                std::cout << "All secrets are already configured.\n";
            }
            return 0;
        }

        if (arg == "--status") {
            std::cout << "Target Database (.env): " << env.current_database() << "\n";
            std::cout << "Shell Database ($ENV):  " << (env.shell_database().empty() ? "(not set)" : env.shell_database()) << "\n";
            std::cout << "Config Path:            " << env.env_path() << "\n";
            return 0;
        }

        std::cerr << "Unknown option: " << arg << "\n";
        print_usage(argv[0]);
        return 1;
    }

    // No arguments: launch interactive menu
    TerminalMenu menu(env);
    return menu.run();
}
