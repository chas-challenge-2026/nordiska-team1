#include "benchmark.hpp"

#include <chrono>
#include <iomanip>
#include <iostream>
#include <thread>

namespace nordiska::benchmark {

BenchmarkRunner::BenchmarkRunner(BenchmarkOptions options)
    : options_(std::move(options)), db_(options_.database) {
    if (options_.database.empty()) {
        options_.database = db_.database();
    }
}

BenchmarkResult BenchmarkRunner::run() {
    BenchmarkResult result;
    result.worker_env = db_.get_worker_environment();

    DbCounts counts = db_.get_counts();
    std::cout << "\n================================================================================\n"
              << "                      NORDISKA REPORTING PIPELINE BENCHMARK\n"
              << "================================================================================\n"
              << "Target Database:      " << db_.database() << "\n"
              << "Current DB Scale:     " << counts.customers << " customers | "
              << counts.accounts << " accounts | " << counts.transactions << " transactions\n"
              << "Reporting Year:       " << options_.year << "\n"
              << "Polling Interval:     " << options_.poll_interval_seconds << " seconds\n";

    if (result.worker_env.found) {
        std::cout << "--------------------------------------------------------------------------------\n"
                  << "BENCHMARKED WORKER ENVIRONMENT:\n"
                  << "  Worker Container:   " << result.worker_env.container_name << " (" << result.worker_env.container_id << ")\n"
                  << "  Runtime & Image:    " << result.worker_env.image << " (.NET " << (result.worker_env.dotnet_version.empty() ? "8.0" : result.worker_env.dotnet_version) << ")\n"
                  << "  Worker Concurrency: " << result.worker_env.concurrency << " threads (TaxReportWorker__Concurrency)\n"
                  << "  PDF Socket Signing: " << (result.worker_env.pdf_enable_signing == "1" ? "Enabled (1)" : "Disabled (0)") << " (NORDISKA_PDF_ENABLE_SIGNING)\n"
                  << "  PDF Signer Socket:  " << (result.worker_env.pdf_signer_socket.empty() ? "None" : result.worker_env.pdf_signer_socket) << "\n"
                  << "  Storage Volume:     " << (result.worker_env.storage_volume.empty() ? "local" : result.worker_env.storage_volume)
                  << " -> " << (result.worker_env.storage_root.empty() ? "/var/lib/nordiska/report-documents" : result.worker_env.storage_root) << "\n"
                  << "  Worker DB Target:   " << (result.worker_env.db_name.empty() ? db_.database() : result.worker_env.db_name)
                  << " (Host: " << (result.worker_env.db_host.empty() ? "db" : result.worker_env.db_host)
                  << ", User: " << (result.worker_env.db_user.empty() ? "nordiska_reporting_worker" : result.worker_env.db_user) << ")\n";
    }

    if (options_.clean_first) {
        std::cout << "Cleaning previous reporting snapshots, jobs, and generated documents...\n";
        db_.clean_reporting_data(true);
    }

    // -------------------------------------------------------------------------
    // PHASE 1: STAGING (JOB INJECTION)
    // -------------------------------------------------------------------------
    std::cout << "\n--- PHASE 1: STAGING (JOB INJECTION) ---\n"
              << "Injecting jobs with AvailableAt = future (held)..." << std::endl;

    auto stage_start = std::chrono::steady_clock::now();

    // 1. Tax reports (1 per customer)
    auto t0 = std::chrono::steady_clock::now();
    result.tax_jobs = db_.stage_tax_reports(options_.year, options_.limit);
    auto t1 = std::chrono::steady_clock::now();
    result.tax_stage_ms = std::chrono::duration<double, std::milli>(t1 - t0).count();
    double tax_rate = result.tax_stage_ms > 0 ? (result.tax_jobs / (result.tax_stage_ms / 1000.0)) : 0.0;
    std::cout << "[+] Staged " << result.tax_jobs << " Tax Report Jobs in "
              << std::fixed << std::setprecision(1) << result.tax_stage_ms << " ms ("
              << std::setprecision(0) << tax_rate << " jobs/s)\n";

    // 2. Account statements (1 per account)
    auto t2 = std::chrono::steady_clock::now();
    result.statement_jobs = db_.stage_account_statements(options_.year, options_.limit);
    auto t3 = std::chrono::steady_clock::now();
    result.statement_stage_ms = std::chrono::duration<double, std::milli>(t3 - t2).count();
    double stmt_rate = result.statement_stage_ms > 0 ? (result.statement_jobs / (result.statement_stage_ms / 1000.0)) : 0.0;
    std::cout << "[+] Staged " << result.statement_jobs << " Statement Jobs + Entries in "
              << std::fixed << std::setprecision(1) << result.statement_stage_ms << " ms ("
              << std::setprecision(0) << stmt_rate << " jobs/s)\n";

    result.total_staged = result.tax_jobs + result.statement_jobs;
    result.total_stage_ms = std::chrono::duration<double, std::milli>(t3 - stage_start).count();
    result.staging_jobs_per_sec = result.total_stage_ms > 0 ? (result.total_staged / (result.total_stage_ms / 1000.0)) : 0.0;

    std::cout << ">>> Staging Total: " << result.total_staged << " jobs staged in "
              << std::fixed << std::setprecision(1) << result.total_stage_ms << " ms ("
              << std::setprecision(0) << result.staging_jobs_per_sec << " jobs/sec)\n";

    if (result.total_staged == 0) {
        std::cerr << "[-] Error: No jobs were staged. Check database contents.\n";
        return result;
    }

    // -------------------------------------------------------------------------
    // PHASE 2: DRAIN EXECUTION (THE STARTING GUN)
    // -------------------------------------------------------------------------
    std::cout << "\n--- PHASE 2: DRAIN EXECUTION (THE STARTING GUN) ---\n"
              << "[!] Firing off the machine! Releasing " << result.total_staged
              << " jobs simultaneously...\n";

    // The starting gun: release all jobs
    auto exec_start = std::chrono::steady_clock::now();
    db_.release_all_jobs();

    std::cout << "[!] Batch released. Polling worker drain progress every "
              << options_.poll_interval_seconds << "s...\n";

    JobProgress last_progress;
    while (true) {
        std::this_thread::sleep_for(std::chrono::seconds(options_.poll_interval_seconds));

        last_progress = db_.poll_job_progress();
        auto now = std::chrono::steady_clock::now();
        double elapsed_sec = std::chrono::duration<double>(now - exec_start).count();

        double pct = result.total_staged > 0 ? (100.0 * last_progress.completed / result.total_staged) : 0.0;
        double cur_throughput = elapsed_sec > 0 ? (last_progress.completed / elapsed_sec) : 0.0;

        std::cout << "\r[" << std::fixed << std::setprecision(1) << elapsed_sec << "s] Completed: "
                  << last_progress.completed << " / " << result.total_staged
                  << " (" << std::setprecision(1) << pct << "%) | Processing: "
                  << last_progress.processing << " | Pending: " << last_progress.pending
                  << " | " << std::setprecision(1) << cur_throughput << " PDFs/s"
                  << std::flush;

        if (last_progress.active() == 0) {
            std::cout << "\n";
            break;
        }
    }

    auto exec_end = std::chrono::steady_clock::now();
    result.wall_clock_sec = std::chrono::duration<double>(exec_end - exec_start).count();
    result.completed_jobs = last_progress.completed;
    result.failed_jobs = last_progress.failed;
    result.pdfs_per_sec = result.wall_clock_sec > 0 ? (result.completed_jobs / result.wall_clock_sec) : 0.0;

    // Post-run stats
    result.latency = db_.query_latency_stats();
    result.documents_in_db = db_.count_generated_documents();

    print_summary(result, options_);
    return result;
}

void BenchmarkRunner::print_summary(const BenchmarkResult& r, const BenchmarkOptions& opt) {
    std::cout << "\n================================================================================\n"
              << "                              BENCHMARK SUMMARY\n"
              << "================================================================================\n"
              << "Target Database:         " << opt.database << "\n";
    if (r.worker_env.found) {
        std::cout << "Benchmarked Worker:      Concurrency: " << r.worker_env.concurrency
                  << " | Signing: " << (r.worker_env.pdf_enable_signing == "1" ? "Enabled" : "Disabled")
                  << " | .NET " << (r.worker_env.dotnet_version.empty() ? "8.0" : r.worker_env.dotnet_version)
                  << " | " << r.worker_env.container_name << "\n"
                  << "PDF Storage Volume:      " << (r.worker_env.storage_volume.empty() ? "local" : r.worker_env.storage_volume) << "\n";
    }
    std::cout << "Total Documents Staged:  " << r.total_staged << " (" << r.tax_jobs << " tax + " << r.statement_jobs << " statements)\n"
              << "--------------------------------------------------------------------------------\n"
              << "PHASE 1: STAGING (JOB INJECTION)\n"
              << "  Tax Jobs Staging:      " << std::fixed << std::setprecision(1) << r.tax_stage_ms << " ms\n"
              << "  Statement Jobs Staging:" << std::fixed << std::setprecision(1) << r.statement_stage_ms << " ms\n"
              << "  Total Injection Time:  " << std::fixed << std::setprecision(1) << r.total_stage_ms << " ms\n"
              << "  Injection Throughput:  " << std::fixed << std::setprecision(0) << r.staging_jobs_per_sec << " jobs/sec\n"
              << "--------------------------------------------------------------------------------\n"
              << "PHASE 2: DRAIN EXECUTION (PRODUCTION .NET WORKER)\n"
              << "  Wall-Clock Duration:   " << std::fixed << std::setprecision(2) << r.wall_clock_sec << " s\n"
              << "  Completed PDFs:        " << r.completed_jobs << "\n"
              << "  Failed Jobs:           " << r.failed_jobs << "\n"
              << "  Wall-Clock Throughput: " << std::fixed << std::setprecision(1) << r.pdfs_per_sec << " PDFs/sec\n"
              << "  DB Span (Min->Max):    " << std::fixed << std::setprecision(2) << r.latency.db_span_sec << " s";

    if (r.latency.db_span_sec > 0) {
        double db_throughput = r.completed_jobs / r.latency.db_span_sec;
        std::cout << " (" << std::fixed << std::setprecision(1) << db_throughput << " PDFs/sec db-measured)\n";
    } else {
        std::cout << "\n";
    }

    std::cout << "  Document Latencies:    P50: " << std::fixed << std::setprecision(1) << r.latency.p50_ms << " ms | "
              << "P95: " << r.latency.p95_ms << " ms | "
              << "P99: " << r.latency.p99_ms << " ms | "
              << "Avg: " << r.latency.avg_ms << " ms\n"
              << "  Verified in DB:        " << r.documents_in_db << " documents in reporting.generated_documents\n"
              << "--------------------------------------------------------------------------------\n"
              << "TOTAL END-TO-END BATCH TIME (Staging + Drain): "
              << std::fixed << std::setprecision(2) << ((r.total_stage_ms / 1000.0) + r.wall_clock_sec) << " s\n"
              << "================================================================================\n"
              << std::endl;
}

} // namespace nordiska::benchmark
