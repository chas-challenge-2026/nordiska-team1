#pragma once
#include "nordiska/report.hpp"
#include <filesystem>
namespace nordiska {
// This is the application's PDF boundary. No PDF-library types should cross it.
class IPdfRenderer {
  public:
    virtual ~IPdfRenderer() = default;
    virtual void render(const Report& report, const std::filesystem::path& output_path) = 0;
};
} // namespace nordiska
