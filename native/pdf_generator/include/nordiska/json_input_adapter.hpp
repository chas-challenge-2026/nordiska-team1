#pragma once
#include "nordiska/input_adapter.hpp"

namespace nordiska {

class JsonInputAdapter final : public IInputAdapter {
  public:
    Report import(const std::filesystem::path& input_path) const override;
};

} // namespace nordiska
