#pragma once

#include "nordiska/domain/report.hpp"
#include "nordiska/ports/document_renderer.hpp"
#include "nordiska/ports/output_destination.hpp"

#include <cstddef>
#include <functional>
#include <memory>
#include <span>
#include <string>
#include <vector>

namespace nordiska {

struct DocumentRequest {
    Report report;
};

enum class DocumentFailure {
    none,
    invalid_input,
    generation,
};

struct DocumentResult {
    std::size_t index{};
    bool succeeded{};
    DocumentFailure failure{DocumentFailure::none};
    std::string error;
};

class GenerateDocuments {
  public:
    using RendererFactory = std::function<std::unique_ptr<IDocumentRenderer>()>;

    explicit GenerateDocuments(RendererFactory renderer_factory, std::size_t worker_count = 0);

    std::vector<DocumentResult> execute(std::span<const DocumentRequest> requests,
                                        IOutputDestination& destination) const;

  private:
    RendererFactory renderer_factory_;
    std::size_t worker_count_;
};

} // namespace nordiska
