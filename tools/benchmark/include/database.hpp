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
    std::string min_started_at;
    std::string max_completed_at;
};

struct WorkerEnvironment {
    bool found = false;
    std::string container_id;
    std::string container_name;
    std::string image;
    std::string dotnet_version;
    std::string concurrency = "12";
    std::string signer_concurrency = "128";
    std::string pdf_enable_signing = "0";
    std::string pdf_signer_mode = "plain";
    std::string pdf_signer_socket = "/run/pdf-signer/pdf-signer.sock";
    std::string storage_root = "/var/lib/nordiska/report-documents";
    std::string storage_volume;
    std::string db_host;
    std::string db_name;
    std::string db_user;
    std::string db_connection_masked;
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
    WorkerEnvironment get_worker_environment() const;
    bool is_tsa_proxy_needed(int& min_ms, int& max_ms) const;
    bool is_tsa_proxy_running() const;
    bool start_tsa_proxy(int min_ms = 2, int max_ms = 20) const;
    void stop_tsa_proxy() const;
    void ensure_tsa_proxy_if_needed(int min_ms = 2, int max_ms = 20) const;

    const std::filesystem::path& repo_root() const { return repo_root_; }

    std::string get_env_setting(const std::string& key, const std::string& default_val = "") const;
    void apply_environment_changes(const std::string& key, const std::string& val);
    void recreate_worker_service() const;

    static std::string detect_active_database();
    static std::filesystem::path find_repo_root();

private:
    std::filesystem::path repo_root_;
    std::string database_;
    std::string container_;
    std::string find_db_container() const;
};

} // namespace nordiska::benchmark
