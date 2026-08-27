#pragma once

#include "nordiska/byte_sink.hpp"
#include "nordiska/pdf_renderer.hpp"
#include "nordiska/report.hpp"

#include <memory>

namespace nordiska {

enum class PdfRendererKind {
    haru,
    cairo,
};

// Renderer construction is composition policy, not delivery-adapter policy.
std::unique_ptr<IPdfRenderer> make_pdf_renderer(PdfRendererKind renderer_kind);

// Production composition for callers that require the default PDF output.
// Haru is selected here so delivery adapters do not choose a PDF engine.
void generate_default_pdf(const Report& report, IByteSink& sink);

} // namespace nordiska
