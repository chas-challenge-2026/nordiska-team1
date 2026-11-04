#include "nordiska/rendering/pdf_engine.hpp"

#include "nordiska/rendering/cairo_pdf_engine.hpp"
#include "nordiska/rendering/haru_pdf_engine.hpp"
#include "nordiska/rendering/native_pdf_engine.hpp"

#include <cmath>
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

std::expected<std::vector<uint8_t>, RenderError> PdfEngine::render(const DocumentLayout& layout,
                                                                   size_t tail_capacity) const {
    for (const auto& page : layout.pages) {
        for (const auto& command : page.commands) {
            if (const auto* placed = std::get_if<PositionedImage>(&command)) {
                const auto* image = placed->image;
                if (!image || image->width == 0 || image->height == 0 || image->width > 32767 ||
                    image->height > 32767 ||
                    static_cast<uint64_t>(image->width) * image->height * 3 != image->rgb.size() ||
                    !std::isfinite(placed->width) || !std::isfinite(placed->height) || placed->width <= 0 ||
                    placed->height <= 0) {
                    return std::unexpected(
                        RenderError{RenderErrorKind::EngineError, DiagnosticCode::InvalidRGBImageResource});
                }
            }
        }
    }
    return impl_->render(layout, tail_capacity);
}

} // namespace nordiska
