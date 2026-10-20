#pragma once

#include <filesystem>
#include <optional>
#include <string>
#include <vector>
#include <utility>

namespace nordiska::env {

namespace fs = std::filesystem;

struct EnvEntry {
    bool is_comment_or_blank = false;
    std::string raw_line;
    std::string key;
    std::string value;
};

class EnvFile {
public:
    EnvFile();

    static fs::path find_repo_root();

    bool load(const fs::path& custom_path = "");
    bool save(const fs::path& custom_path = "");

    std::optional<std::string> get(const std::string& key) const;
    void set(const std::string& key, const std::string& value);
    bool remove(const std::string& key);
    bool has(const std::string& key) const;

    // Database convenience helper
    void set_database(const std::string& db_name);
    std::string current_database() const;
    std::string shell_database() const;

    // Retrieval
    const std::vector<EnvEntry>& entries() const { return entries_; }
    std::vector<std::pair<std::string, std::string>> all_vars() const;

    // Paths
    fs::path repo_root() const { return repo_root_; }
    fs::path env_path() const { return env_path_; }
    fs::path export_path() const { return export_path_; }

    // Shell export string
    std::string generate_export_commands() const;
    void write_export_file() const;

private:
    fs::path repo_root_;
    fs::path env_path_;
    fs::path export_path_;
    std::vector<EnvEntry> entries_;

    static std::string trim(const std::string& s);
    void parse_line(const std::string& line);
};

} // namespace nordiska::env
