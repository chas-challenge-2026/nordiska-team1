#pragma once

#include "nordiska/ports/document_renderer.hpp"

#include <memory>

namespace nordiska {

enum class PdfEngine {
    haru,
    cairo,
};

class PdfRenderer final : public IDocumentRenderer {
  public:
    ~PdfRenderer() override;
    void render(const Report& report, IByteSink& sink) override;

  private:
    class Impl;
    explicit PdfRenderer(std::unique_ptr<Impl> implementation);

    friend std::unique_ptr<PdfRenderer> make_pdf_renderer(PdfEngine engine);
    std::unique_ptr<Impl> implementation_;
};

std::unique_ptr<PdfRenderer> make_pdf_renderer(PdfEngine engine);

} // namespace nordiska
