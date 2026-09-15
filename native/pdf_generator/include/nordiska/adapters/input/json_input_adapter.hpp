#pragma once

#include "nordiska/domain/report.hpp"

#include <filesystem>
#include <stdexcept>
#include <string_view>
#include <vector>

namespace nordiska {

class JsonInputError final : public std::runtime_error {
  public:
    using std::runtime_error::runtime_error;
};

class JsonInputAdapter final {
  public:
    Report import(const std::filesystem::path& input_path) const;
    Report import_text(std::string_view json) const;

    std::vector<Report> import_reports(const std::filesystem::path& input_path) const;
    std::vector<Report> import_reports_text(std::string_view json) const;
};

} // namespace nordiska
