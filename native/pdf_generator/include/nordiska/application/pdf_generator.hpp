#pragma once

#include "nordiska/domain/generated_pdfs.hpp"
#include "nordiska/ingestion/json_ingestor.hpp"
#include "nordiska/rendering/pdf_engine.hpp"
#include "nordiska/signing/pdf_signer.hpp"

#include <cstdint>
#include <expected>
#include <memory>
#include <span>
#include <string>

namespace nordiska {

struct GeneratorConfig {
    JsonIngestorKind ingestor{JsonIngestorKind::Simdjson};
    PdfEngineKind engine{PdfEngineKind::Libharu};
    bool enable_signing{false};
    bool compression{true};
    std::shared_ptr<PdfSigner> custom_signer{nullptr};
};

struct PipelineTiming {
    double ingest_seconds{0.0};
    double layout_seconds{0.0};
    double render_seconds{0.0};
    double sign_seconds{0.0};

    [[nodiscard]] double total_seconds() const noexcept {
        return ingest_seconds + layout_seconds + render_seconds + sign_seconds;
    }
};

enum class GeneratorErrorKind {
    InvalidArgument,
    InvalidInput,
    ResourceLimitExceeded,
    SigningError,
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
    std::shared_ptr<PdfSigner> signer_;
    bool enable_signing_{false};
};

} // namespace nordiska
