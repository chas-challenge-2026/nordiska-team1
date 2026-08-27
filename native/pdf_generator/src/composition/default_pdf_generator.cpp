#include "nordiska/default_pdf_generator.hpp"

#include "nordiska/cairo_pdf_renderer.hpp"
#include "nordiska/create_pdf.hpp"
#include "nordiska/libharu_pdf_renderer.hpp"

#include <memory>

namespace nordiska {

std::unique_ptr<IPdfRenderer> make_pdf_renderer(PdfRendererKind renderer_kind) {
    if (renderer_kind == PdfRendererKind::haru) {
        return std::make_unique<LibHaruPdfRenderer>();
    }
    return std::make_unique<CairoPdfRenderer>();
}

void generate_default_pdf(const Report& report, IByteSink& sink) {
    std::unique_ptr<IPdfRenderer> renderer = make_pdf_renderer(PdfRendererKind::haru);
    CreatePdf(*renderer).execute(report, sink);
}

} // namespace nordiska
