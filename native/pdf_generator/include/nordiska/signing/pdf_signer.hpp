#pragma once

#include <array>
#include <cstdint>
#include <expected>
#include <memory>
#include <span>
#include <string>
#include <string_view>

namespace nordiska {

using Sha256Digest = std::array<uint8_t, 32>;

enum class SigningErrorKind {
    InvalidArgument,
    InvalidPdf,
    KeyError,
    SignatureGenerationFailed,
    InvalidSignatureOutput,
    SignatureTooLarge,
    DigestFailed,
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

    // When requested, accumulate only the external call duration in call_seconds.
    // Return CMS hex only. The generator owns PDF structure, hashing and insertion.
    [[nodiscard]] virtual std::expected<std::string, SigningError>
    sign_digest(std::span<const uint8_t, 32> digest, const SigningContext& context, double* call_seconds = nullptr) = 0;
};

// Creates one owning C signer adapter for reuse across documents. No global state.
[[nodiscard]] std::expected<std::unique_ptr<PdfSigner>, SigningError> create_native_pdf_signer();

} // namespace nordiska
