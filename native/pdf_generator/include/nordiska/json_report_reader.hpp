#pragma once
#include "nordiska/report.hpp"
#include <filesystem>
namespace nordiska {
Report read_report_json(const std::filesystem::path& input_path);
} // namespace nordiska
