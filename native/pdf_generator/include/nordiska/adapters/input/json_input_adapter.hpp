#pragma once

#include "nordiska/domain/report.hpp"

#include <filesystem>
#include <stdexcept>
#include <string_view>

namespace nordiska {

class JsonInputError final : public std::runtime_error {
  public:
    using std::runtime_error::runtime_error;
};

class JsonInputAdapter final {
  public:
    Report import(const std::filesystem::path& input_path) const;
    Report import_text(std::string_view json) const;
};

} // namespace nordiska
