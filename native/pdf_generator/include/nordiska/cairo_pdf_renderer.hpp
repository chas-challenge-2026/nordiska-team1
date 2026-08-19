#pragma once

#include "nordiska/pdf_renderer.hpp"

namespace nordiska {

class CairoPdfRenderer final : public IPdfRenderer {
  public:
    void render(const Report& report, const std::filesystem::path& output_path) override;
};

} // namespace nordiska
