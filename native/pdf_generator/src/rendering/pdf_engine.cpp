#include "nordiska/rendering/pdf_engine.hpp"
#include "pdf_engine_impl.hpp"

#include <memory>
#include <vector>

namespace nordiska {

PdfEngine::PdfEngine(PdfEngineConfig config) {
    switch (config.kind) {
    case PdfEngineKind::Libharu:
        impl_ = make_haru_engine(config.compression);
        break;
    case PdfEngineKind::Cairo:
        impl_ = make_cairo_engine();
        break;
    case PdfEngineKind::Native:
        impl_ = make_native_engine(config.compression);
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
