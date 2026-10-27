#include "benchmark.hpp"

#include <chrono>
#include <ctime>
#include <filesystem>
#include <fstream>
#include <iomanip>
#include <iostream>
#include <thread>

namespace fs = std::filesystem;

namespace nordiska::benchmark {

namespace {

std::string current_iso_utc() {
    auto now = std::chrono::system_clock::now();
    auto ms = std::chrono::duration_cast<std::chrono::milliseconds>(now.time_since_epoch()) % 1000;
    std::time_t t = std::chrono::system_clock::to_time_t(now);
    std::tm tm{};
    gmtime_r(&t, &tm);
    char buf[64];
    std::snprintf(buf, sizeof(buf), "%04d-%02d-%02d %02d:%02d:%02d.%03d UTC",
                  tm.tm_year + 1900, tm.tm_mon + 1, tm.tm_mday,
                  tm.tm_hour, tm.tm_min, tm.tm_sec, (int)ms.count());
    return std::string(buf);
}

std::string timestamp_slug() {
    auto now = std::chrono::system_clock::now();
    std::time_t t = std::chrono::system_clock::to_time_t(now);
    std::tm tm{};
    localtime_r(&t, &tm);
    char buf[64];
    std::snprintf(buf, sizeof(buf), "%04d%02d%02d_%02d%02d%02d",
                  tm.tm_year + 1900, tm.tm_mon + 1, tm.tm_mday,
                  tm.tm_hour, tm.tm_min, tm.tm_sec);
    return std::string(buf);
}

} // namespace

BenchmarkRunner::BenchmarkRunner(BenchmarkOptions options)
    : options_(std::move(options)), db_(options_.database) {
    if (options_.database.empty()) {
        options_.database = db_.database();
    }
}

BenchmarkResult BenchmarkRunner::run() {
    BenchmarkResult result;
    result.run_timestamp = current_iso_utc();
    result.worker_env = db_.get_worker_environment();

    DbCounts counts = db_.get_counts();
    std::cout << "\n================================================================================\n"
              << "                      NORDISKA REPORTING PIPELINE BENCHMARK\n"
              << "================================================================================\n"
              << "Target Database:      " << db_.database() << " (" << counts.customers << " customers, " << counts.accounts << " accounts)\n";

    if (result.worker_env.found) {
        std::string sign_str = (result.worker_env.pdf_enable_signing == "1")
            ? (result.worker_env.pdf_signer_mode == "timestamp" ? "Timestamped (RFC 3161)" : "Plain CMS")
            : "Disabled (Unsigned)";
        std::cout << "Worker Configuration: " << result.worker_env.concurrency << " worker threads | "
                  << result.worker_env.signer_concurrency << " signer threads | " << sign_str << "\n";
    }

    if (options_.clean_first) {
        db_.clean_reporting_data(true);
    }

    // TSA Jitter / Latency Proxy Lifecycle
    int min_ms = 2, max_ms = 20;
    bool proxy_needed = false;
    if (options_.jitter_min.has_value() || options_.jitter_max.has_value()) {
        proxy_needed = true;
        min_ms = options_.jitter_min.value_or(2);
        max_ms = options_.jitter_max.value_or(std::max(min_ms, 20));
    } else {
        proxy_needed = db_.is_tsa_proxy_needed(min_ms, max_ms);
    }

    bool started_proxy = false;
    if (proxy_needed) {
        min_ms = std::clamp(min_ms, 0, 1000);
        max_ms = std::clamp(max_ms, min_ms, 1000);
        if (db_.start_tsa_proxy(min_ms, max_ms)) {
            started_proxy = true;
            std::cout << "TSA Network Proxy:    Active (" << min_ms << "–" << max_ms << " ms simulated latency)\n";
        }
    }
    std::cout << "--------------------------------------------------------------------------------\n";

    try {
    std::cout << "[1/2] Staging:        Injecting jobs into database..." << std::flush;

    result.stage_start_time = current_iso_utc();
    auto stage_start = std::chrono::steady_clock::now();

    // 1. Tax reports (1 per customer)
    auto t0 = std::chrono::steady_clock::now();
    result.tax_jobs = db_.stage_tax_reports(options_.year, options_.limit);
    auto t1 = std::chrono::steady_clock::now();
    result.tax_stage_ms = std::chrono::duration<double, std::milli>(t1 - t0).count();

    // 2. Account statements (1 per account)
    auto t2 = std::chrono::steady_clock::now();
    result.statement_jobs = db_.stage_account_statements(options_.year, options_.limit);
    auto t3 = std::chrono::steady_clock::now();
    result.statement_stage_ms = std::chrono::duration<double, std::milli>(t3 - t2).count();

    result.total_staged = result.tax_jobs + result.statement_jobs;
    result.total_stage_ms = std::chrono::duration<double, std::milli>(t3 - stage_start).count();
    result.staging_jobs_per_sec = result.total_stage_ms > 0 ? (result.total_staged / (result.total_stage_ms / 1000.0)) : 0.0;
    result.stage_end_time = current_iso_utc();

    std::cout << "\r[1/2] Staging:        Staged " << result.total_staged << " jobs ("
              << result.tax_jobs << " tax, " << result.statement_jobs << " statements) in "
              << std::fixed << std::setprecision(1) << (result.total_stage_ms / 1000.0) << "s ("
              << std::setprecision(0) << result.staging_jobs_per_sec << " jobs/s)\n";

    if (result.total_staged == 0) {
        std::cerr << "[-] Error: No jobs were staged. Check database contents.\n";
        return result;
    }

    std::cout << "[2/2] Running:        Releasing batch to " << result.worker_env.concurrency << " worker threads...\n";

    // The starting gun: release all jobs
    result.drain_start_time = current_iso_utc();
    auto exec_start = std::chrono::steady_clock::now();
    db_.release_all_jobs();

    JobProgress last_progress;
    while (true) {
        std::this_thread::sleep_for(std::chrono::seconds(options_.poll_interval_seconds));

        last_progress = db_.poll_job_progress();
        auto now = std::chrono::steady_clock::now();
        double elapsed_sec = std::chrono::duration<double>(now - exec_start).count();

        double pct = result.total_staged > 0 ? (100.0 * last_progress.completed / result.total_staged) : 0.0;
        double cur_throughput = elapsed_sec > 0 ? (last_progress.completed / elapsed_sec) : 0.0;

        int bar_width = 24;
        int filled = std::clamp(static_cast<int>(pct / 100.0 * bar_width), 0, bar_width);
        std::string bar;
        for (int i = 0; i < filled; ++i) bar += "=";
        if (filled < bar_width) bar += ">";
        while (bar.size() < static_cast<size_t>(bar_width)) bar += " ";

        std::ostringstream ss;
        ss << "\r[2/2] Running:        [" << bar << "] "
           << std::fixed << std::setprecision(1) << pct << "% | "
           << last_progress.completed << " / " << result.total_staged << " | "
           << std::setprecision(1) << cur_throughput << " PDFs/s | "
           << std::setprecision(0) << elapsed_sec << "s";
        std::string line_out = ss.str();
        if (line_out.size() < 79) line_out.append(79 - line_out.size(), ' ');
        std::cout << line_out << std::flush;

        if (last_progress.active() == 0) {
            std::cout << "\n";
            break;
        }
    }

    auto exec_end = std::chrono::steady_clock::now();
    result.drain_end_time = current_iso_utc();
    result.wall_clock_sec = std::chrono::duration<double>(exec_end - exec_start).count();
    result.completed_jobs = last_progress.completed;
    result.failed_jobs = last_progress.failed;
    result.pdfs_per_sec = result.wall_clock_sec > 0 ? (result.completed_jobs / result.wall_clock_sec) : 0.0;

    // Post-run stats
    result.latency = db_.query_latency_stats();
    result.documents_in_db = db_.count_generated_documents();

    if (started_proxy && !options_.keep_proxy) {
        db_.stop_tsa_proxy();
        started_proxy = false;
    }

    print_summary(result, options_);
    if (options_.save_report) {
        std::string report_path = save_report(result, options_, db_.repo_root());
        if (!report_path.empty()) {
            std::cout << "Report saved to: " << report_path << " (latest: tools/benchmark/runs/latest.txt)\n\n";
        }
    }
    return result;
    } catch (...) {
        if (started_proxy && !options_.keep_proxy) {
            db_.stop_tsa_proxy();
        }
        throw;
    }
}

void BenchmarkRunner::print_summary(const BenchmarkResult& r, const BenchmarkOptions& opt) {
    write_summary(std::cout, r, opt);
}

void BenchmarkRunner::write_summary(std::ostream& os, const BenchmarkResult& r, const BenchmarkOptions& opt) {
    std::string sign_str = (r.worker_env.pdf_enable_signing == "1")
        ? (r.worker_env.pdf_signer_mode == "timestamp" ? "Timestamped (RFC 3161)" : "Plain CMS")
        : "Disabled (Unsigned)";

    os << "\n=========================== BENCHMARK RESULTS ===========================\n"
       << "  Target Database:       " << opt.database << "\n"
       << "  Worker Configuration:  " << r.worker_env.concurrency << " threads | " << sign_str;
    if (opt.jitter_min.has_value() || opt.jitter_max.has_value()) {
        os << " (TSA Latency: " << opt.jitter_min.value_or(0) << "–" << opt.jitter_max.value_or(0) << " ms)";
    }
    os << "\n  -----------------------------------------------------------------------\n"
       << "  Throughput:            " << std::fixed << std::setprecision(1) << r.pdfs_per_sec << " PDFs/sec (wall-clock)\n";
    if (r.latency.db_span_sec > 0) {
        double db_tput = r.completed_jobs / r.latency.db_span_sec;
        os << "  DB-Measured Rate:      " << std::fixed << std::setprecision(1) << db_tput << " PDFs/sec (span: "
           << std::setprecision(2) << r.latency.db_span_sec << "s)\n";
    }
    os << "  Total Batch Time:      " << std::fixed << std::setprecision(2)
       << ((r.total_stage_ms / 1000.0) + r.wall_clock_sec) << "s (Staging: "
       << std::setprecision(1) << (r.total_stage_ms / 1000.0) << "s | Drain: "
       << std::setprecision(1) << r.wall_clock_sec << "s)\n"
       << "  Documents Processed:   " << r.completed_jobs << " / " << r.total_staged << " completed";
    if (r.failed_jobs > 0) {
        os << " (" << r.failed_jobs << " failed)";
    }
    os << "\n"
       << "  Document Latencies:    P50: " << std::fixed << std::setprecision(1) << r.latency.p50_ms << "ms | "
       << "P95: " << r.latency.p95_ms << "ms | "
       << "P99: " << r.latency.p99_ms << "ms | "
       << "Avg: " << r.latency.avg_ms << "ms\n"
       << "=========================================================================\n";
}

void BenchmarkRunner::write_mechanical_data(std::ostream& os, const BenchmarkResult& r, const BenchmarkOptions& opt) {
    os << "\n# ==============================================================================\n"
       << "# MECHANICAL BENCHMARK DATA (KEY-VALUE)\n"
       << "# ==============================================================================\n"
       << "[benchmark_metadata]\n"
       << "run_timestamp = " << r.run_timestamp << "\n"
       << "target_database = " << opt.database << "\n"
       << "reporting_year = " << opt.year << "\n"
       << "customer_limit = " << opt.limit << "\n"
       << "\n[worker_environment]\n"
       << "worker_container_name = " << r.worker_env.container_name << "\n"
       << "worker_container_id = " << r.worker_env.container_id << "\n"
       << "worker_concurrency = " << r.worker_env.concurrency << "\n"
       << "signer_concurrency = " << r.worker_env.signer_concurrency << "\n"
       << "pdf_signing_enabled = " << r.worker_env.pdf_enable_signing << "\n"
       << "pdf_signer_mode = " << r.worker_env.pdf_signer_mode << "\n"
       << "pdf_signer_socket = " << (r.worker_env.pdf_signer_socket.empty() ? "/run/pdf-signer/pdf-signer.sock" : r.worker_env.pdf_signer_socket) << "\n"
       << "storage_root = " << (r.worker_env.storage_root.empty() ? "/var/lib/nordiska/report-documents" : r.worker_env.storage_root) << "\n"
       << "storage_volume = " << (r.worker_env.storage_volume.empty() ? "local" : r.worker_env.storage_volume) << "\n"
       << "dotnet_version = " << (r.worker_env.dotnet_version.empty() ? "8.0" : r.worker_env.dotnet_version) << "\n"
       << "tsa_latency_min_ms = " << (opt.jitter_min.has_value() ? std::to_string(*opt.jitter_min) : "default") << "\n"
       << "tsa_latency_max_ms = " << (opt.jitter_max.has_value() ? std::to_string(*opt.jitter_max) : "default") << "\n"
       << "\n[phase1_staging]\n"
       << "tax_jobs = " << r.tax_jobs << "\n"
       << "statement_jobs = " << r.statement_jobs << "\n"
       << "total_staged = " << r.total_staged << "\n"
       << "stage_start_time = " << r.stage_start_time << "\n"
       << "stage_end_time = " << r.stage_end_time << "\n"
       << "staging_total_ms = " << std::fixed << std::setprecision(1) << r.total_stage_ms << "\n"
       << "tax_stage_ms = " << std::fixed << std::setprecision(1) << r.tax_stage_ms << "\n"
       << "statement_stage_ms = " << std::fixed << std::setprecision(1) << r.statement_stage_ms << "\n"
       << "staging_throughput_jobs_sec = " << std::fixed << std::setprecision(1) << r.staging_jobs_per_sec << "\n"
       << "\n[phase2_drain]\n"
       << "drain_start_time = " << r.drain_start_time << "\n"
       << "drain_end_time = " << r.drain_end_time << "\n"
       << "wall_clock_sec = " << std::fixed << std::setprecision(3) << r.wall_clock_sec << "\n"
       << "completed_pdfs = " << r.completed_jobs << "\n"
       << "failed_jobs = " << r.failed_jobs << "\n"
       << "wall_clock_throughput_pdfs_sec = " << std::fixed << std::setprecision(2) << r.pdfs_per_sec << "\n"
       << "db_span_sec = " << std::fixed << std::setprecision(3) << r.latency.db_span_sec << "\n"
       << "db_throughput_pdfs_sec = " << std::fixed << std::setprecision(2) << (r.latency.db_span_sec > 0 ? (r.completed_jobs / r.latency.db_span_sec) : 0.0) << "\n"
       << "\n[latencies]\n"
       << "latency_p50_ms = " << std::fixed << std::setprecision(2) << r.latency.p50_ms << "\n"
       << "latency_p95_ms = " << std::fixed << std::setprecision(2) << r.latency.p95_ms << "\n"
       << "latency_p99_ms = " << std::fixed << std::setprecision(2) << r.latency.p99_ms << "\n"
       << "latency_avg_ms = " << std::fixed << std::setprecision(2) << r.latency.avg_ms << "\n"
       << "verified_documents_in_db = " << r.documents_in_db << "\n"
       << "\n[totals]\n"
       << "total_end_to_end_sec = " << std::fixed << std::setprecision(3) << ((r.total_stage_ms / 1000.0) + r.wall_clock_sec) << "\n"
       << "# ==============================================================================\n";
}

std::string BenchmarkRunner::save_report(const BenchmarkResult& r, const BenchmarkOptions& opt, const fs::path& repo_root) {
    try {
        fs::path runs_dir;
        if (!opt.runs_dir.empty()) {
            runs_dir = opt.runs_dir;
        } else {
            runs_dir = repo_root / "tools/benchmark/runs";
        }
        fs::create_directories(runs_dir);

        std::string slug = timestamp_slug();
        fs::path report_file = runs_dir / ("run_" + slug + ".txt");
        fs::path latest_file = runs_dir / "latest.txt";

        {
            std::ofstream out(report_file);
            if (out.is_open()) {
                write_summary(out, r, opt);
                write_mechanical_data(out, r, opt);
            }
        }

        {
            std::ofstream latest_out(latest_file);
            if (latest_out.is_open()) {
                write_summary(latest_out, r, opt);
                write_mechanical_data(latest_out, r, opt);
            }
        }

        return report_file.string();
    } catch (const std::exception& e) {
        std::cerr << "Warning: Failed to save mechanical benchmark report: " << e.what() << "\n";
        return "";
    }
}

} // namespace nordiska::benchmark
