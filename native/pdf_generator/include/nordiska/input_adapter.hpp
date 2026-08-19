#pragma once

#include "nordiska/report.hpp"

#include <filesystem>

namespace nordiska {

class IInputAdapter {
  public:
    virtual ~IInputAdapter() = default;
    virtual Report import(const std::filesystem::path& input_path) const = 0;
};

} // namespace nordiska
