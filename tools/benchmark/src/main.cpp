#include "benchmark.hpp"

#include <algorithm>
#include <csignal>
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
              << "  --year <YYYY>        Tax & statement reporting year (default: 2026)\n"
              << "  --poll <seconds>     Drain progress polling interval in seconds (default: 2)\n"
              << "  --limit <N>          Limit number of customers/accounts (default: 0 = all)\n"
              << "  --no-clean           Do not truncate reporting tables before the run\n"
              << "  --clean              Clean reporting tables before run (default)\n"
              << "  --jitter <ms|min-max> Simulated TSA latency (up to 1000ms, e.g. 500 or 100-1000)\n"
              << "  --jitter-min <ms>    Minimum TSA latency in ms (0–1000)\n"
              << "  --jitter-max <ms>    Maximum TSA latency in ms (0–1000)\n"
              << "  --tsa-jitter <min> <max> TSA latency range in ms (0–1000)\n"
              << "  --keep-proxy         Keep TSA proxy running after benchmark finishes\n"
              << "  --start-proxy        Start TSA proxy with configured jitter and exit\n"
              << "  --stop-proxy         Stop running TSA proxy and exit\n"
              << "  --help, -h           Show this help message\n\n"
              << "Examples:\n"
              << "  " << prog << "\n"
              << "  " << prog << " --limit 100 --jitter 50-500\n"
              << "  " << prog << " --jitter 1000\n"
              << "  " << prog << " --start-proxy --jitter 100-1000\n"
              << "  " << prog << " --stop-proxy\n";
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
        } else if (arg == "--jitter" && i + 1 < argc) {
            std::string j_val = argv[++i];
            auto dash = j_val.find('-');
            if (dash != std::string::npos) {
                int min_j = std::clamp(std::stoi(j_val.substr(0, dash)), 0, 1000);
                int max_j = std::clamp(std::stoi(j_val.substr(dash + 1)), min_j, 1000);
                options.jitter_min = min_j;
                options.jitter_max = max_j;
            } else {
                int max_j = std::clamp(std::stoi(j_val), 0, 1000);
                options.jitter_min = 0;
                options.jitter_max = max_j;
            }
        } else if (arg == "--jitter-min" && i + 1 < argc) {
            options.jitter_min = std::clamp(std::stoi(argv[++i]), 0, 1000);
        } else if (arg == "--jitter-max" && i + 1 < argc) {
            options.jitter_max = std::clamp(std::stoi(argv[++i]), 0, 1000);
        } else if (arg == "--tsa-jitter" && i + 2 < argc) {
            int min_j = std::clamp(std::stoi(argv[++i]), 0, 1000);
            int max_j = std::clamp(std::stoi(argv[++i]), min_j, 1000);
            options.jitter_min = min_j;
            options.jitter_max = max_j;
        } else if (arg == "--keep-proxy") {
            options.keep_proxy = true;
        } else if (arg == "--start-proxy") {
            options.start_proxy_only = true;
        } else if (arg == "--stop-proxy") {
            options.stop_proxy_only = true;
        } else {
            std::cerr << "Unknown option: " << arg << "\n";
            print_help(argv[0]);
            return 1;
        }
    }

    if (options.stop_proxy_only) {
        nordiska::benchmark::DatabaseClient client(options.database);
        std::cout << "[+] Stopping TSA Latency Proxy...\n";
        client.stop_tsa_proxy();
        std::cout << "[+] TSA Latency Proxy stopped.\n";
        return 0;
    }

    if (options.start_proxy_only) {
        nordiska::benchmark::DatabaseClient client(options.database);
        int min_ms = options.jitter_min.value_or(2);
        int max_ms = options.jitter_max.value_or(std::max(min_ms, 20));
        min_ms = std::clamp(min_ms, 0, 1000);
        max_ms = std::clamp(max_ms, min_ms, 1000);
        std::cout << "[+] Starting TSA Latency Proxy (127.0.0.1:8082 -> 8081, " << min_ms << "–" << max_ms << " ms jitter)...\n";
        if (client.start_tsa_proxy(min_ms, max_ms)) {
            std::cout << "[+] TSA Latency Proxy successfully started.\n";
            return 0;
        } else {
            std::cerr << "[-] Failed to start TSA Latency Proxy.\n";
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
