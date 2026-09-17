#pragma once

#include "nordiska/rendering/pdf_engine.hpp"

#include <memory>

namespace nordiska {

std::unique_ptr<PdfEngine::Impl> create_native_pdf_engine(bool compression);

} // namespace nordiska
