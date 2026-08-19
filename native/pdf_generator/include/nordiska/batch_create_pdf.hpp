#pragma once

#include "nordiska/create_pdf.hpp"

#include <cstddef>
#include <functional>
#include <memory>
#include <vector>

namespace nordiska {

struct BatchPdfRequest {
    Report report;
    std::filesystem::path output_path;
};

using PdfRendererFactory = std::function<std::unique_ptr<IPdfRenderer>()>;

class BatchCreatePdf {
  public:
    explicit BatchCreatePdf(PdfRendererFactory renderer_factory, std::size_t worker_count = 0);

    void execute(const std::vector<BatchPdfRequest>& requests) const;

  private:
    PdfRendererFactory renderer_factory_;
    std::size_t worker_count_;
};

} // namespace nordiska
