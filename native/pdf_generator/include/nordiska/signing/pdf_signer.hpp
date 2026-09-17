#pragma once

#include <cstdint>
#include <expected>
#include <memory>
#include <span>
#include <string>
#include <string_view>
#include <vector>

namespace nordiska {

enum class SigningErrorKind {
    InvalidArgument,
    KeyError,
    SignatureGenerationFailed,
    ResourceLimitExceeded,
    InternalError,
};

struct SigningError {
    SigningErrorKind kind{SigningErrorKind::InternalError};
    std::string message;
};

struct SigningContext {
    std::string_view document_id;
    uint64_t customer_id{0};
};

class PdfSigner {
  public:
    virtual ~PdfSigner() = default;

    [[nodiscard]] virtual std::expected<std::vector<uint8_t>, SigningError> sign(std::span<const uint8_t> unsigned_pdf,
                                                                                 const SigningContext& context) = 0;
};

// Default stub implementation representing the ready-to-wire seam
// until native/pdf-signer implements full PDF byte-level PKCS#7 signature embedding.
class StubPdfSigner : public PdfSigner {
  public:
    explicit StubPdfSigner(bool simulate_failure = false, std::string failure_document_id = "")
        : simulate_failure_(simulate_failure), failure_document_id_(std::move(failure_document_id)) {}

    [[nodiscard]] std::expected<std::vector<uint8_t>, SigningError> sign(std::span<const uint8_t> unsigned_pdf,
                                                                         const SigningContext& context) override;

  private:
    bool simulate_failure_{false};
    std::string failure_document_id_;
};

} // namespace nordiska
