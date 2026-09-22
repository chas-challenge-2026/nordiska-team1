#pragma once

#include "nordiska/domain/generated_pdfs.hpp"
#include "nordiska/ingestion/json_ingestor.hpp"
#include "nordiska/rendering/pdf_engine.hpp"
#include "nordiska/signing/pdf_signer.hpp"

#include <cstdint>
#include <expected>
#include <memory>
#include <optional>
#include <span>
#include <string>

namespace nordiska {

struct GeneratorConfig {
    JsonIngestorKind ingestor{JsonIngestorKind::Simdjson};
    PdfEngineKind engine{PdfEngineKind::Libharu};
    bool enable_signing{false};
    bool compression{true};
    std::shared_ptr<PdfSigner> custom_signer{nullptr};
    // Hex characters, excluding delimiters. Passed per render/preparation call.
    size_t signature_contents_capacity{kDefaultSignatureSlotSize};
};

// For "--instrumented" arg  in CLI/benchmark
struct PipelineTiming {
    double ingest_seconds{0.0};
    double layout_seconds{0.0};
    double render_seconds{0.0};
    double sign_seconds{0.0};        // End-to-end signing phase, including preparation and insertion.
    double hash_seconds{0.0};        // Subset: signing digest plus final artifact checksum.
    double signer_call_seconds{0.0}; // Subset: only the external signing function call.

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
    SignaturePreparationFailed,
    HashingFailed,
    InvalidSignatureOutput,
    SignatureTooLarge,
};

// Wrap in struct so std::expected can return both
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
    size_t signature_contents_capacity_;
    std::optional<SigningError> signer_initialization_error_;

    [[nodiscard]] static GeneratorError map_signing_error(const SigningError& error);
    [[nodiscard]] std::unexpected<GeneratorError> map_signing_failure(const SigningError& error,
                                                                      std::string_view document_id) const;
};

} // namespace nordiska
