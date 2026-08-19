#pragma once

#include "nordiska/pdf_renderer.hpp"

namespace nordiska {

class CairoPdfRenderer final : public IPdfRenderer {
  public:
    void render(const Report& report, IByteSink& sink) override;
};

} // namespace nordiska
