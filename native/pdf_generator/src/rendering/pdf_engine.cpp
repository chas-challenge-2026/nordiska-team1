#include "nordiska/rendering/pdf_engine.hpp"

#include "nordiska/rendering/cairo_pdf_engine.hpp"
#include "nordiska/rendering/haru_pdf_engine.hpp"
#include "nordiska/rendering/native_pdf_engine.hpp"

#include <memory>
#include <vector>

namespace nordiska {

PdfEngine::PdfEngine(PdfEngineConfig config) {
    switch (config.kind) {
    case PdfEngineKind::Libharu:
        impl_ = create_haru_pdf_engine(config.compression);
        break;
    case PdfEngineKind::Cairo:
        impl_ = create_cairo_pdf_engine();
        break;
    case PdfEngineKind::Native:
        impl_ = create_native_pdf_engine(config.compression);
        break;
    }
}

PdfEngine::~PdfEngine() = default;

PdfEngine::PdfEngine(PdfEngine&&) noexcept = default;
PdfEngine& PdfEngine::operator=(PdfEngine&&) noexcept = default;

std::expected<std::vector<uint8_t>, RenderError> PdfEngine::render(const DocumentLayout& layout) const {
    return impl_->render(layout);
}

} // namespace nordiska
