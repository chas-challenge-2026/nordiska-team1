#pragma once

#include "nordiska/rendering/pdf_engine.hpp"

#include <memory>

namespace nordiska {

std::unique_ptr<PdfEngine::Impl> create_cairo_pdf_engine();

} // namespace nordiska
