#include "nordiska/rendering/pdf_engine.hpp"

namespace nordiska {

// Stub implementation: layout and rendering engine is not yet implemented (scheduled for Step 3).
PdfEngine::PdfEngine(PdfEngineKind kind) : kind_(kind) {}

PdfEngine::~PdfEngine() = default;

PdfEngine::PdfEngine(PdfEngine&&) noexcept = default;
PdfEngine& PdfEngine::operator=(PdfEngine&&) noexcept = default;

} // namespace nordiska
