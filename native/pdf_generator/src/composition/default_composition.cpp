#include "nordiska/composition/default_composition.hpp"

#include "nordiska/adapters/renderers/pdf/pdf_renderer.hpp"

namespace nordiska {

std::unique_ptr<IDocumentRenderer> make_default_document_renderer() {
    return make_pdf_renderer(PdfEngine::haru);
}

} // namespace nordiska
