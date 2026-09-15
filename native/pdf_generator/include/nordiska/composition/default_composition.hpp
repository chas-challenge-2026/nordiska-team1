#pragma once

#include "nordiska/ports/document_renderer.hpp"

#include <memory>

namespace nordiska {

std::unique_ptr<IDocumentRenderer> make_default_document_renderer();

} // namespace nordiska
