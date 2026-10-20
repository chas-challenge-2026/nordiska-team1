#pragma once

#include <cstdint>
#include <string>
#include <vector>
#include <filesystem>
#include <optional>

namespace nordiska::benchmark {

struct DbCounts {
    int64_t customers = 0;
    int64_t accounts = 0;
    int64_t transactions = 0;
    int64_t documents = 0;
};

struct JobProgress {
    int64_t pending = 0;
    int64_t processing = 0;
    int64_t completed = 0;
    int64_t failed = 0;

    int64_t active() const { return pending + processing; }
    int64_t total() const { return pending + processing + completed + failed; }
};

struct LatencyStats {
    double p50_ms = 0.0;
    double p95_ms = 0.0;
    double p99_ms = 0.0;
    double avg_ms = 0.0;
    double db_span_sec = 0.0;
};

class DatabaseClient {
public:
    explicit DatabaseClient(std::string database = "");

    const std::string& database() const { return database_; }
    const std::string& container() const { return container_; }

    std::string sql(const std::string& query) const;
    DbCounts get_counts() const;
    void clean_reporting_data(bool clean_storage = true) const;

    int64_t stage_tax_reports(int year, int64_t limit = 0) const;
    int64_t stage_account_statements(int year, int64_t limit = 0) const;
    void release_all_jobs() const;

    JobProgress poll_job_progress() const;
    LatencyStats query_latency_stats() const;
    int64_t count_generated_documents() const;

    static std::string detect_active_database();

private:
    std::filesystem::path repo_root_;
    std::string database_;
    std::string container_;
    std::string find_db_container() const;
};

} // namespace nordiska::benchmark
