#pragma once

#include "nordiska/rendering/pdf_engine.hpp"

#include <memory>

namespace nordiska {

struct PdfEngine::Impl {
    virtual ~Impl() = default;
    [[nodiscard]] virtual std::expected<std::vector<uint8_t>, RenderError>
    render(const DocumentLayout& layout) const = 0;
};

std::unique_ptr<PdfEngine::Impl> make_haru_engine(bool compression);
std::unique_ptr<PdfEngine::Impl> make_cairo_engine();
std::unique_ptr<PdfEngine::Impl> make_native_engine(bool compression);

} // namespace nordiska
