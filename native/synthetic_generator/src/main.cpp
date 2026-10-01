#include "generator.hpp"

#include <iostream>
#include <vector>
#include <string>
#include <string_view>
#include <chrono>
#include <thread>
#include <mutex>
#include <atomic>
#include <format>
#include <cstring>
#include <cstdio>
#include <cmath>
#include <memory>
#include <algorithm>
#include <unordered_set>

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

struct PsqlPipe {
    FILE* fp{nullptr};
    std::mutex mtx;

    bool open(const std::string& cmd) {
        fp = popen(cmd.c_str(), "w");
        return fp != nullptr;
    }

    void write_data(std::string_view data) {
        std::lock_guard<std::mutex> lock(mtx);
        if (fp && !data.empty()) {
            fwrite(data.data(), 1, data.size(), fp);
        }
    }

    int close() {
        std::lock_guard<std::mutex> lock(mtx);
        if (fp) {
            int ret = pclose(fp);
            fp = nullptr;
            return ret;
        }
        return 0;
    }

    ~PsqlPipe() {
        if (fp) {
            pclose(fp);
        }
    }
};

static inline void print_progress_bar(const std::string& stage, double progress, int64_t current, int64_t total, double rate, double eta_sec) {
    int bar_width = 30;
    int filled = std::clamp(static_cast<int>(progress * bar_width), 0, bar_width);
    std::string bar = "";
    for (int i = 0; i < filled; ++i) bar += "█";
    for (int i = filled; i < bar_width; ++i) bar += "░";

    std::string eta_str = (eta_sec >= 0.0) ? std::format("{:.0f}s", eta_sec) : "--";
    std::cerr << std::format("\r\033[K\033[36m[{}]\033[0m \033[32m[{}]\033[0m {:5.1f}% | {:>12} / {:>12} ({:>12} rows/s) | ETA: {:>4}",
        stage, bar, progress * 100.0, format_num(current), format_num(total), format_num(static_cast<int64_t>(rate)), eta_str);
    std::cerr.flush();
}

int main(int argc, char** argv) {
    GeneratorConfig cfg;
    std::string container = "nordiska-db-1";
    std::string dbname = "nordiska_synthetic";
    bool benchmark_mode = false;
    bool drop_indexes = false;

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
        } else if (arg == "--stress-text") {
            cfg.stress_text = true;
        } else if (arg == "--benchmark") {
            benchmark_mode = true;
        } else if (arg == "--drop-indexes") {
            drop_indexes = true;
        }
    }

    std::cout << std::format("\033[1;36mNordiska Fast Native Generator (C++23)\033[0m\n");
    std::cout << std::format("  Customers:    {:>12}\n", format_num(cfg.customer_count));
    std::cout << std::format("  CPU Threads:  {:>12}\n", cfg.thread_count);
    std::cout << std::format("  Target DB:    {:>12}\n", dbname);
    std::cout << std::format("  Mode:         {:>12}\n\n", benchmark_mode ? "Benchmark (Dry-run)" : "Direct PostgreSQL COPY Stream");

    // Pre-pass: partition customers across threads
    struct ThreadPartition {
        int64_t cust_start{0};
        int64_t cust_end{0};
    };

    std::vector<ThreadPartition> partitions(cfg.thread_count);
    int64_t custs_per_thread = cfg.customer_count / cfg.thread_count;
    int64_t custs_rem = cfg.customer_count % cfg.thread_count;

    int64_t curr_c = cfg.start_customer_id;
    for (int t = 0; t < cfg.thread_count; ++t) {
        int64_t count = custs_per_thread + (t < custs_rem ? 1 : 0);
        partitions[t].cust_start = curr_c;
        partitions[t].cust_end = curr_c + count;
        curr_c += count;
    }

    // Atomic global ID allocators ensuring zero collisions
    std::atomic<int64_t> global_account_id{cfg.start_account_id};
    std::atomic<int64_t> global_ledger_id{cfg.start_ledger_id};

    auto start_time = std::chrono::steady_clock::now();

    // 1. Stream Customers & AspNetUserRoles
    PsqlPipe cust_pipe;
    PsqlPipe role_pipe;
    if (!benchmark_mode) {
        std::string cust_cmd = std::format(
            "docker exec -i {} psql -U nordiska_bootstrap -d {} -v ON_ERROR_STOP=1 -q -c "
            "\"COPY banking.customers (\\\"Id\\\", \\\"PersonalNum\\\", \\\"Name\\\", \\\"UserName\\\", \\\"NormalizedUserName\\\", \\\"Email\\\", \\\"NormalizedEmail\\\", \\\"PhoneNumber\\\", \\\"PasswordHash\\\", \\\"SecurityStamp\\\", \\\"ConcurrencyStamp\\\", \\\"EmailConfirmed\\\", \\\"PhoneNumberConfirmed\\\", \\\"TwoFactorEnabled\\\", \\\"LockoutEnabled\\\", \\\"AccessFailedCount\\\", \\\"CreatedAt\\\") FROM STDIN WITH (FORMAT text, DELIMITER E'\\t');\"",
            container, dbname);
        cust_pipe.open(cust_cmd);

        std::string role_cmd = std::format(
            "docker exec -i {} psql -U nordiska_bootstrap -d {} -v ON_ERROR_STOP=1 -q -c "
            "\"COPY banking.\\\"AspNetUserRoles\\\" (\\\"UserId\\\", \\\"RoleId\\\") FROM STDIN WITH (FORMAT text, DELIMITER E'\\t');\"",
            container, dbname);
        role_pipe.open(role_cmd);
    }

    std::atomic<int64_t> customers_done{0};
    std::mutex pn_mtx;
    std::unordered_set<std::string> used_personal_nums;
    auto cust_start_time = std::chrono::steady_clock::now();

    // Launch progress reporting thread
    std::atomic<bool> progress_running{true};
    std::jthread progress_thread([&]() {
        while (progress_running) {
            int64_t done = customers_done.load();
            double progress = cfg.customer_count > 0 ? static_cast<double>(done) / cfg.customer_count : 1.0;
            auto now = std::chrono::steady_clock::now();
            double elapsed = std::chrono::duration<double>(now - cust_start_time).count();
            double rate = elapsed > 0.05 ? done / elapsed : 0.0;
            double eta = rate > 0.0 ? (cfg.customer_count - done) / rate : -1.0;
            print_progress_bar("Customers", progress, done, cfg.customer_count, rate, eta);
            std::this_thread::sleep_for(std::chrono::milliseconds(100));
        }
    });

    std::vector<std::jthread> cust_threads;
    for (int t = 0; t < cfg.thread_count; ++t) {
        cust_threads.emplace_back([&, t]() {
            std::string cust_buf;
            std::string role_buf;
            cust_buf.reserve(256 * 1024);
            role_buf.reserve(64 * 1024);

            for (int64_t c = partitions[t].cust_start; c < partitions[t].cust_end; ++c) {
                FastRng rng(stable_seed(cfg.master_seed, c));
                
                std::string pnum;
                while (true) {
                    std::string cand = generate_personnummer(rng);
                    std::lock_guard<std::mutex> lk(pn_mtx);
                    if (used_personal_nums.insert(cand).second) {
                        pnum = std::move(cand);
                        break;
                    }
                }

                std::string fname = "Kalle";
                std::string lname = "Svensson";
                std::string full_name = fname + " " + lname;
                std::string email = std::format("cust{}@nordiska-demo.se", c);
                std::string norm_email = std::format("CUST{}@NORDISKA-DEMO.SE", c);
                std::string phone = std::format("+4670{:07d}", rng.randint(1000000, 9999999));
                std::string created_str = std::format("{}-01-01 08:00:00+00", cfg.year);

                cust_buf += std::format(
                    "{}\t{}\t{}\t{}\t{}\t{}\t{}\t{}\t{}\t\t\tt\tf\tf\tf\t0\t{}\n",
                    c, pnum, full_name, email, norm_email, email, norm_email, phone,
                    "AQAAAAEAACcQAAAAEPI/M0/zVJYqy70AD5XmxCGusa4f003trDYZVeUzZHaVGSFDDNwRIMlWeLlSS6WCqg==",
                    created_str
                );
                role_buf += std::format("{}\t2\n", c);

                if (cust_buf.size() >= 128 * 1024) {
                    if (!benchmark_mode) {
                        cust_pipe.write_data(cust_buf);
                        role_pipe.write_data(role_buf);
                    }
                    cust_buf.clear();
                    role_buf.clear();
                }
                customers_done.fetch_add(1, std::memory_order_relaxed);
            }

            if (!cust_buf.empty() && !benchmark_mode) {
                cust_pipe.write_data(cust_buf);
                role_pipe.write_data(role_buf);
            }
        });
    }
    cust_threads.clear();
    progress_running = false;
    if (progress_thread.joinable()) progress_thread.join();
    cust_pipe.close();
    role_pipe.close();

    std::cerr << "\n";
    std::cout << std::format("✓ Streamed {:>12} customers\n", format_num(cfg.customer_count));

    // 2. Stream Accounts & Ledger Entries
    PsqlPipe acct_pipe;
    PsqlPipe ledger_pipe;
    if (!benchmark_mode) {
        std::string acct_cmd = std::format(
            "docker exec -i {} psql -U nordiska_bootstrap -d {} -v ON_ERROR_STOP=1 -q -c "
            "\"COPY banking.savings_accounts (\\\"Id\\\", \\\"CustomerId\\\", \\\"AccountNumber\\\", \\\"AccountName\\\", \\\"AccountType\\\", \\\"Balance\\\", \\\"InterestRate\\\", \\\"Status\\\", \\\"CreatedAt\\\") FROM STDIN WITH (FORMAT text, DELIMITER E'\\t');\"",
            container, dbname);
        acct_pipe.open(acct_cmd);

        std::string ledger_cmd = std::format(
            "docker exec -i {} psql -U nordiska_bootstrap -d {} -v ON_ERROR_STOP=1 -q -c "
            "\"COPY banking.ledger_entries (\\\"Id\\\", \\\"AccountId\\\", \\\"Type\\\", \\\"Amount\\\", \\\"Label\\\", \\\"TargetAccountId\\\", \\\"IsPlanned\\\", \\\"CreatedAt\\\") FROM STDIN WITH (FORMAT text, DELIMITER E'\\t');\"",
            container, dbname);
        ledger_pipe.open(ledger_cmd);
    }

    std::atomic<int64_t> transactions_done{0};
    int64_t est_total_txs = cfg.customer_count * 65;
    auto tx_start_time = std::chrono::steady_clock::now();
    progress_running = true;

    std::jthread tx_progress_thread([&]() {
        while (progress_running) {
            int64_t done = transactions_done.load();
            double progress = est_total_txs > 0 ? static_cast<double>(done) / est_total_txs : 1.0;
            if (progress > 1.0) progress = 1.0;
            auto now = std::chrono::steady_clock::now();
            double elapsed = std::chrono::duration<double>(now - tx_start_time).count();
            double rate = elapsed > 0.05 ? done / elapsed : 0.0;
            double eta = rate > 0.0 ? (std::max(int64_t{0}, est_total_txs - done)) / rate : -1.0;
            print_progress_bar("Ledger Tx", progress, done, est_total_txs, rate, eta);
            std::this_thread::sleep_for(std::chrono::milliseconds(100));
        }
    });

    std::vector<std::jthread> worker_threads;
    for (int t = 0; t < cfg.thread_count; ++t) {
        worker_threads.emplace_back([&, t]() {
            std::string acct_buf;
            std::string ledger_buf;
            acct_buf.reserve(256 * 1024);
            ledger_buf.reserve(1024 * 1024);

            for (int64_t c = partitions[t].cust_start; c < partitions[t].cust_end; ++c) {
                FastRng rng(stable_seed(cfg.master_seed, c));
                int64_t a_count = pareto_sample(rng, 1.0, 1.9, 6.0);

                for (int64_t a_idx = 0; a_idx < a_count; ++a_idx) {
                    int64_t acct_id = global_account_id.fetch_add(1);
                    double rate = 0.035;
                    std::string acct_type = "flex";
                    std::string acct_name = "Sparkonto Flex";
                    std::string acct_num = std::format("NOR-{:06d}", acct_id);
                    double balance = 0.0;

                    double opening_amount = static_cast<double>(rng.randint(15000, 180000));
                    balance += opening_amount;

                    int64_t opening_tx_id = global_ledger_id.fetch_add(1);
                    ledger_buf += std::format(
                        "{}\t{}\tdeposit\t{:.2f}\tÖppningsinsättning\t\\N\tf\t{}-01-01 09:00:00+00\n",
                        opening_tx_id, acct_id, opening_amount, cfg.year
                    );
                    transactions_done.fetch_add(1, std::memory_order_relaxed);

                    int64_t tx_count = pareto_sample(rng, 6.0, 1.45, 400.0);
                    double sim_balance = opening_amount;
                    double sim_accrued = 0.0;

                    // Monthly simulation for fast vectorization
                    for (int m = 1; m <= 12; ++m) {
                        int txs_in_month = static_cast<int>(tx_count / 12) + (rng.randint(0, 2));
                        for (int k = 0; k < txs_in_month; ++k) {
                            int day = static_cast<int>(rng.randint(1, 28));
                            bool is_dep = (sim_balance < 5000.0) || (rng.next_double() < 0.45);
                            int64_t tx_id = global_ledger_id.fetch_add(1);
                            if (is_dep) {
                                double amt = std::round((rng.randint(200, 35000) + rng.next_double()) * 100.0) / 100.0;
                                sim_balance += amt;
                                ledger_buf += std::format(
                                    "{}\t{}\tdeposit\t{:.2f}\tLön från Volvo Group AB\t\\N\tf\t{}-{:02d}-{:02d} 12:00:00+00\n",
                                    tx_id, acct_id, amt, cfg.year, m, day
                                );
                            } else {
                                double max_w = std::max(100.0, sim_balance * 0.7);
                                double amt = std::round((rng.randint(50, static_cast<int64_t>(std::min(20000.0, max_w))) + rng.next_double()) * 100.0) / 100.0;
                                sim_balance -= amt;
                                ledger_buf += std::format(
                                    "{}\t{}\twithdrawal\t{:.2f}\tKortköp på ICA Supermarket\t\\N\tf\t{}-{:02d}-{:02d} 14:00:00+00\n",
                                    tx_id, acct_id, -amt, cfg.year, m, day
                                );
                            }
                            transactions_done.fetch_add(1, std::memory_order_relaxed);
                        }

                        // Interest accrual & capitalization on month-end
                        if (sim_balance > 0.0) {
                            sim_accrued += (sim_balance * rate * 30.0) / 365.0;
                        }
                        double gross_credit = std::round(sim_accrued * 100.0) / 100.0;
                        sim_accrued = 0.0;

                        if (gross_credit >= 0.01) {
                            double tax_withheld = std::round(gross_credit * 0.30 * 100.0) / 100.0;
                            int64_t int_id = global_ledger_id.fetch_add(1);
                            int64_t tax_id = global_ledger_id.fetch_add(1);
                            ledger_buf += std::format(
                                "{}\t{}\tinterest\t{:.2f}\tRänteutbetalning\t\\N\tf\t{}-{:02d}-28 23:59:50+00\n",
                                int_id, acct_id, gross_credit, cfg.year, m
                            );
                            ledger_buf += std::format(
                                "{}\t{}\ttax\t{:.2f}\tPreliminärskatt 30%\t\\N\tf\t{}-{:02d}-28 23:59:55+00\n",
                                tax_id, acct_id, -tax_withheld, cfg.year, m
                            );
                            sim_balance += (gross_credit - tax_withheld);
                            transactions_done.fetch_add(2, std::memory_order_relaxed);
                        }
                    }

                    acct_buf += std::format(
                        "{}\t{}\t{}\t{}\t{}\t{:.2f}\t{:.6f}\tactive\t{}-01-01 08:30:00+00\n",
                        acct_id, c, acct_num, acct_name, acct_type, sim_balance, rate, cfg.year
                    );

                    if (ledger_buf.size() >= 512 * 1024) {
                        if (!benchmark_mode) {
                            ledger_pipe.write_data(ledger_buf);
                            acct_pipe.write_data(acct_buf);
                        }
                        ledger_buf.clear();
                        acct_buf.clear();
                    }
                }
            }

            if (!ledger_buf.empty() && !benchmark_mode) {
                ledger_pipe.write_data(ledger_buf);
                acct_pipe.write_data(acct_buf);
            }
        });
    }
    worker_threads.clear();
    progress_running = false;
    if (tx_progress_thread.joinable()) tx_progress_thread.join();
    acct_pipe.close();
    ledger_pipe.close();

    std::cerr << "\n";
    auto end_time = std::chrono::steady_clock::now();
    double total_sec = std::chrono::duration<double>(end_time - start_time).count();
    int64_t total_tx_done = transactions_done.load();
    int64_t total_accts_done = global_account_id.load() - cfg.start_account_id;

    std::cout << std::format("\n\033[1;32m✓ Ingested {:>12} transactions across {:>12} accounts in {:.2f}s ({:>12} tx/s)\033[0m\n",
        format_num(total_tx_done), format_num(total_accts_done), total_sec, format_num(static_cast<int64_t>(total_tx_done / std::max(0.001, total_sec))));

    // Reset sequences in PostgreSQL
    if (!benchmark_mode) {
        std::string seq_cmd = std::format(
            "docker exec -i {} psql -U nordiska_bootstrap -d {} -q -c \""
            "SELECT setval(pg_get_serial_sequence('banking.customers', 'Id'), COALESCE((SELECT MAX(\\\"Id\\\") FROM banking.customers), 1)); "
            "SELECT setval(pg_get_serial_sequence('banking.savings_accounts', 'Id'), COALESCE((SELECT MAX(\\\"Id\\\") FROM banking.savings_accounts), 1)); "
            "SELECT setval(pg_get_serial_sequence('banking.ledger_entries', 'Id'), COALESCE((SELECT MAX(\\\"Id\\\") FROM banking.ledger_entries), 1));\"",
            container, dbname);
        system(seq_cmd.c_str());
    }

    return 0;
}
