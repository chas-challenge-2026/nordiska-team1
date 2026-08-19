#pragma once

#include <algorithm>
#include <cstdint>
#include <filesystem>
#include <regex>
#include <string>
#include <string_view>

namespace nordiska::cli {

inline std::filesystem::path reports_directory() {
    return std::filesystem::current_path() / "output" / "reports";
}

inline std::int64_t next_report_number(const std::filesystem::path& directory,
                                      std::string_view renderer) {
    std::filesystem::create_directories(directory);
    const std::regex report_pattern("^report-" + std::string(renderer) + R"(-([0-9]+)\.pdf$)");
    std::int64_t next_number = 0;

    for (const auto& entry : std::filesystem::directory_iterator(directory)) {
        if (!entry.is_regular_file()) {
            continue;
        }
        std::smatch match;
        const std::string filename = entry.path().filename().string();
        if (!std::regex_match(filename, match, report_pattern)) {
            continue;
        }
        const std::int64_t candidate = static_cast<std::int64_t>(std::stoll(match[1].str())) + 1;
        next_number = std::max(next_number, candidate);
    }

    return next_number;
}

inline std::filesystem::path report_path(const std::filesystem::path& directory,
                                         std::string_view renderer, std::int64_t number) {
    return directory / ("report-" + std::string(renderer) + "-" + std::to_string(number) + ".pdf");
}

inline std::filesystem::path next_report_path(const std::filesystem::path& directory,
                                              std::string_view renderer) {
    return report_path(directory, renderer, next_report_number(directory, renderer));
}

} // namespace nordiska::cli
