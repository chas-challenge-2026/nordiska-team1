#include "env_file.hpp"

#include <fstream>
#include <sstream>
#include <iostream>
#include <algorithm>
#include <cstdlib>
#include <stdexcept>
#include <unistd.h>

namespace nordiska::env {

EnvFile::EnvFile() {
    repo_root_ = find_repo_root();
    env_path_ = repo_root_ / "infra/.env";
    export_path_ = repo_root_ / "infra/.env.export";
}

fs::path EnvFile::find_repo_root() {
    // 1. Try from /proc/self/exe
    try {
        if (fs::exists("/proc/self/exe")) {
            fs::path current = fs::read_symlink("/proc/self/exe").parent_path();
            while (!current.empty()) {
                if (fs::exists(current / "infra/docker-compose.yml")) {
                    return current;
                }
                auto parent = current.parent_path();
                if (parent == current) break;
                current = parent;
            }
        }
    } catch (...) {}

    // 2. Try from current working directory
    try {
        fs::path current = fs::current_path();
        while (!current.empty()) {
            if (fs::exists(current / "infra/docker-compose.yml")) {
                return current;
            }
            auto parent = current.parent_path();
            if (parent == current) break;
            current = parent;
        }
    } catch (...) {}

    return fs::current_path();
}

std::string EnvFile::trim(const std::string& s) {
    auto start = s.find_first_not_of(" \t\r\n");
    if (start == std::string::npos) return "";
    auto end = s.find_last_not_of(" \t\r\n");
    return s.substr(start, end - start + 1);
}

void EnvFile::parse_line(const std::string& line) {
    std::string trimmed = trim(line);
    if (trimmed.empty() || trimmed.front() == '#') {
        entries_.push_back({true, line, "", ""});
        return;
    }

    auto eq_pos = line.find('=');
    if (eq_pos == std::string::npos) {
        entries_.push_back({true, line, "", ""});
        return;
    }

    std::string key = trim(line.substr(0, eq_pos));
    std::string val = trim(line.substr(eq_pos + 1));

    // Strip surrounding quotes if present
    if (val.size() >= 2 && ((val.front() == '"' && val.back() == '"') ||
                           (val.front() == '\'' && val.back() == '\''))) {
        val = val.substr(1, val.size() - 2);
    }

    entries_.push_back({false, line, key, val});
}

bool EnvFile::load(const fs::path& custom_path) {
    entries_.clear();

    if (!custom_path.empty()) {
        env_path_ = custom_path;
    } else {
        if (!fs::exists(env_path_)) {
            fs::path legacy_env = repo_root_ / "infra/v2/.env";
            if (fs::exists(legacy_env)) {
                env_path_ = legacy_env;
            } else {
                fs::path template_env = repo_root_ / "infra/.env.template";
                if (fs::exists(template_env)) {
                    // Will read from template, but target is infra/.env
                    std::ifstream infile(template_env);
                    std::string line;
                    while (std::getline(infile, line)) {
                        parse_line(line);
                    }
                    return true;
                }
            }
        }
    }

    std::ifstream infile(env_path_);
    if (!infile.is_open()) {
        return false;
    }

    std::string line;
    while (std::getline(infile, line)) {
        parse_line(line);
    }

    return true;
}

bool EnvFile::save(const fs::path& custom_path) {
    fs::path target = custom_path.empty() ? env_path_ : custom_path;

    // Ensure parent directory exists
    if (!target.parent_path().empty()) {
        fs::create_directories(target.parent_path());
    }

    fs::path temp_file = target.string() + ".tmp";
    std::ofstream outfile(temp_file);
    if (!outfile.is_open()) {
        return false;
    }

    for (const auto& entry : entries_) {
        if (entry.is_comment_or_blank) {
            outfile << entry.raw_line << "\n";
        } else {
            outfile << entry.key << "=" << entry.value << "\n";
        }
    }

    outfile.close();

    // Atomic rename
    std::error_code ec;
    fs::rename(temp_file, target, ec);
    if (ec) {
        // Fallback copy if cross-device or error
        fs::copy_file(temp_file, target, fs::copy_options::overwrite_existing, ec);
        fs::remove(temp_file, ec);
    }

    // Also write shell export script
    write_export_file();

    return true;
}

std::optional<std::string> EnvFile::get(const std::string& key) const {
    for (const auto& entry : entries_) {
        if (!entry.is_comment_or_blank && entry.key == key) {
            return entry.value;
        }
    }
    return std::nullopt;
}

void EnvFile::set(const std::string& key, const std::string& value) {
    for (auto& entry : entries_) {
        if (!entry.is_comment_or_blank && entry.key == key) {
            entry.value = value;
            return;
        }
    }
    // New variable
    entries_.push_back({false, key + "=" + value, key, value});
}

bool EnvFile::remove(const std::string& key) {
    auto it = std::remove_if(entries_.begin(), entries_.end(), [&](const EnvEntry& e) {
        return !e.is_comment_or_blank && e.key == key;
    });
    if (it != entries_.end()) {
        entries_.erase(it, entries_.end());
        return true;
    }
    return false;
}

bool EnvFile::has(const std::string& key) const {
    for (const auto& entry : entries_) {
        if (!entry.is_comment_or_blank && entry.key == key) {
            return true;
        }
    }
    return false;
}

void EnvFile::set_database(const std::string& db_name) {
    set("NORDISKA_DATABASE", db_name);
    if (db_name == "nordiska_synthetic") {
        set("NORDISKA_REPORT_DOCUMENTS_VOLUME", "nordiska-v2_synthetic_report_documents");
    } else if (db_name == "nordiska_v2") {
        set("NORDISKA_REPORT_DOCUMENTS_VOLUME", "nordiska-v2_report_documents");
    }
}

std::string EnvFile::current_database() const {
    auto db = get("NORDISKA_DATABASE");
    return db.value_or("nordiska_v2");
}

std::string EnvFile::shell_database() const {
    const char* val = std::getenv("NORDISKA_DATABASE");
    return val ? std::string(val) : "";
}

std::vector<std::pair<std::string, std::string>> EnvFile::all_vars() const {
    std::vector<std::pair<std::string, std::string>> list;
    for (const auto& entry : entries_) {
        if (!entry.is_comment_or_blank) {
            list.emplace_back(entry.key, entry.value);
        }
    }
    return list;
}

std::string EnvFile::generate_export_commands() const {
    std::ostringstream ss;
    ss << "#!/usr/bin/env bash\n";
    ss << "# Generated by nordiska-env on " << repo_root_.string() << "\n\n";

    for (const auto& entry : entries_) {
        if (!entry.is_comment_or_blank) {
            // Escape double quotes and backslashes
            std::string escaped;
            for (char c : entry.value) {
                if (c == '"' || c == '\\' || c == '$' || c == '`') {
                    escaped += '\\';
                }
                escaped += c;
            }
            ss << "export " << entry.key << "=\"" << escaped << "\"\n";
        }
    }
    return ss.str();
}

void EnvFile::write_export_file() const {
    std::string content = generate_export_commands();

    // 1. Write to export_path_ (infra/.env.export)
    try {
        std::ofstream outfile(export_path_);
        if (outfile.is_open()) {
            outfile << content;
        }
    } catch (...) {}

    // 2. Also write to tools/env/.env.export
    try {
        fs::path tools_export = repo_root_ / "tools/env/.env.export";
        std::ofstream outfile2(tools_export);
        if (outfile2.is_open()) {
            outfile2 << content;
        }
    } catch (...) {}
}

} // namespace nordiska::env
