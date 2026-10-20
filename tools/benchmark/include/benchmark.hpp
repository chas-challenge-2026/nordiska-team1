#pragma once

#include "database.hpp"
#include <string>

namespace nordiska::benchmark {

struct BenchmarkOptions {
    std::string database;
    int year = 2025;
    bool clean_first = true;
    int64_t limit = 0; // 0 = all accounts/customers
    int poll_interval_seconds = 2; // explicitly 2s as requested
    bool interactive = false;
};

struct BenchmarkResult {
    // Phase 1: Staging (Job Injection)
    int64_t tax_jobs = 0;
    int64_t statement_jobs = 0;
    int64_t total_staged = 0;
    double tax_stage_ms = 0.0;
    double statement_stage_ms = 0.0;
    double total_stage_ms = 0.0;
    double staging_jobs_per_sec = 0.0;

    // Phase 2: Drain Execution
    double wall_clock_sec = 0.0;
    double pdfs_per_sec = 0.0;
    int64_t completed_jobs = 0;
    int64_t failed_jobs = 0;
    int64_t documents_in_db = 0;
    LatencyStats latency;
    WorkerEnvironment worker_env;
};

class BenchmarkRunner {
public:
    explicit BenchmarkRunner(BenchmarkOptions options);

    BenchmarkResult run();
    static void print_summary(const BenchmarkResult& result, const BenchmarkOptions& options);

private:
    BenchmarkOptions options_;
    DatabaseClient db_;
};

} // namespace nordiska::benchmark
