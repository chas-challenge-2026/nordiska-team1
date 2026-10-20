#include "benchmark.hpp"

#include <iostream>
#include <string>
#include <vector>
#include <unistd.h>

namespace {

void print_help(const char* prog) {
    std::cout << "Nordiska Whole-Database Reporting Benchmark\n\n"
              << "Usage: " << prog << " [options]\n\n"
              << "Options:\n"
              << "  --db <name>          Database name (default: auto-detect from infra/.env or nordiska_synthetic)\n"
              << "  --year <YYYY>        Tax & statement reporting year (default: 2025)\n"
              << "  --poll <seconds>     Drain progress polling interval in seconds (default: 2)\n"
              << "  --limit <N>          Limit number of customers/accounts (default: 0 = all)\n"
              << "  --no-clean           Do not truncate reporting tables before the run\n"
              << "  --clean              Clean reporting tables before run (default)\n"
              << "  --help, -h           Show this help message\n\n"
              << "Examples:\n"
              << "  " << prog << "\n"
              << "  " << prog << " --limit 100\n"
              << "  " << prog << " --db nordiska_synthetic --poll 2\n";
}

} // namespace

int main(int argc, char* argv[]) {
    nordiska::benchmark::BenchmarkOptions options;

    bool interactive_mode = (argc == 1 && isatty(STDIN_FILENO));

    for (int i = 1; i < argc; ++i) {
        std::string arg = argv[i];
        if (arg == "--help" || arg == "-h") {
            print_help(argv[0]);
            return 0;
        } else if (arg == "--db" && i + 1 < argc) {
            options.database = argv[++i];
        } else if (arg == "--year" && i + 1 < argc) {
            options.year = std::stoi(argv[++i]);
        } else if (arg == "--poll" && i + 1 < argc) {
            options.poll_interval_seconds = std::max(1, std::stoi(argv[++i]));
        } else if (arg == "--limit" && i + 1 < argc) {
            options.limit = std::stoll(argv[++i]);
        } else if (arg == "--no-clean") {
            options.clean_first = false;
        } else if (arg == "--clean") {
            options.clean_first = true;
        } else {
            std::cerr << "Unknown option: " << arg << "\n";
            print_help(argv[0]);
            return 1;
        }
    }

    if (interactive_mode) {
        std::cout << "================================================================================\n"
                  << "            NORDISKA INTERACTIVE REPORTING PIPELINE BENCHMARK\n"
                  << "================================================================================\n";
        try {
            nordiska::benchmark::DatabaseClient probe(options.database);
            auto counts = probe.get_counts();
            auto env = probe.get_worker_environment();
            std::cout << "Detected Active DB:    " << probe.database() << "\n";
            if (env.found) {
                std::cout << "Worker Container:      " << env.container_name << " (ID: " << env.container_id << ")\n"
                          << "Worker Concurrency:    " << env.concurrency << " threads\n"
                          << "PDF Socket Signing:    " << (env.pdf_enable_signing == "1" ? "Enabled (1)" : "Disabled (0)") << "\n";
            }
            std::cout << "Available Customers:   " << counts.customers << "\n"
                      << "Available Customers:   " << counts.customers << "\n"
                      << "Available Accounts:    " << counts.accounts << "\n"
                      << "Ledger Transactions:   " << counts.transactions << "\n"
                      << "Polling Interval:      " << options.poll_interval_seconds << "s\n"
                      << "Reporting Year:        " << options.year << "\n"
                      << "--------------------------------------------------------------------------------\n"
                      << "Press [Enter] to run full benchmark, or type a customer limit (e.g. 100): ";

            std::string line;
            if (std::getline(std::cin, line)) {
                if (!line.empty()) {
                    try {
                        options.limit = std::stoll(line);
                        std::cout << "Limiting benchmark to first " << options.limit << " customers.\n";
                    } catch (...) {
                        std::cout << "Invalid number, running against all customers.\n";
                    }
                }
            }
        } catch (const std::exception& ex) {
            std::cerr << "Warning during pre-flight probe: " << ex.what() << "\n";
        }
    }

    try {
        nordiska::benchmark::BenchmarkRunner runner(options);
        runner.run();
    } catch (const std::exception& ex) {
        std::cerr << "Fatal benchmark error: " << ex.what() << "\n";
        return 1;
    }

    return 0;
}
