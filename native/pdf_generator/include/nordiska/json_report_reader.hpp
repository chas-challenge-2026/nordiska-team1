#pragma once

#include <filesystem>

#include "nordiska/report.hpp"

namespace nordiska {

Report read_report_json(const std::filesystem::path& input_path);

} // namespace nordiska

