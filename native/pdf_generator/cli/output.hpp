#pragma once

#include "nordiska/diagnostics/diagnostic.hpp"
#include "nordiska/ingestion/json_ingestor.hpp"
#include "nordiska/rendering/pdf_engine.hpp"

#include <cstddef>
#include <vector>

namespace nordiska::cli {
struct DocumentStatistics {
    size_t index{};
    size_t bytes{};
};
struct Statistics {
    PdfEngineKind engine{};
    JsonIngestorKind ingestor{};
    double generation_ms{};
    double total_ms{};
    std::vector<DocumentStatistics> documents;
};
void write_error(Diagnostic diagnostic, bool json_summary = false);
void write_document(DocumentStatistics document);
void write_statistics(const Statistics& statistics, bool json_summary, bool verbose);
void write_usage();
void write_version();
} // namespace nordiska::cli
