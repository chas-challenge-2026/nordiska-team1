#pragma once

#include "nordiska/domain/generated_pdfs.hpp"
#include "nordiska/ingestion/json_ingestor.hpp"
#include "nordiska/rendering/pdf_engine.hpp"

#include <cstdint>
#include <expected>
#include <span>
#include <string>

namespace nordiska {

struct GeneratorConfig {
    JsonIngestorKind ingestor{JsonIngestorKind::Nlohmann};
    PdfEngineKind engine{PdfEngineKind::Libharu};
    bool enable_signing{false};
};

struct PipelineTiming {
    double ingest_seconds{0.0};
    double layout_seconds{0.0};
    double render_seconds{0.0};

    [[nodiscard]] double total_seconds() const noexcept {
        return ingest_seconds + layout_seconds + render_seconds;
    }
};

enum class GeneratorErrorKind {
    InvalidArgument,
    InvalidInput,
    ResourceLimitExceeded,
    InternalError,
};

struct GeneratorError {
    GeneratorErrorKind kind{GeneratorErrorKind::InternalError};
    std::string message;
};

class PdfGenerator {
  public:
    explicit PdfGenerator(GeneratorConfig config);
    ~PdfGenerator();

    [[nodiscard]] std::expected<GeneratedPdfs, GeneratorError> generate(std::span<const uint8_t> json_utf8,
                                                                        PipelineTiming* timing = nullptr) const;

  private:
    JsonIngestor ingestor_;
    PdfEngine engine_;
    bool enable_signing_{false};
};

} // namespace nordiska
