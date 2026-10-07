#include "nordiska/signing/pdf_signer.hpp"

#include <chrono>
#include <memory>
#include <pdf_sign.h>
#include <utility>

namespace nordiska {
namespace {

std::string_view status_name(pdf_sign_status_t status) {
    switch (status) {
    case PDF_SIGN_OK:
        return "PDF_SIGN_OK";
    case PDF_SIGN_INVALID_ARGUMENT:
        return "PDF_SIGN_INVALID_ARGUMENT";
    case PDF_SIGN_CERTIFICATE_ERROR:
        return "PDF_SIGN_CERTIFICATE_ERROR";
    case PDF_SIGN_CRYPTO_ERROR:
        return "PDF_SIGN_CRYPTO_ERROR";
    case PDF_SIGN_OUTPUT_ERROR:
        return "PDF_SIGN_OUTPUT_ERROR";
    case PDF_SIGN_UNAUTHORIZED:
        return "PDF_SIGN_UNAUTHORIZED";
    case PDF_SIGN_INTERNAL_ERROR:
        return "PDF_SIGN_INTERNAL_ERROR";
    }
    return "unknown status";
}

SigningError signer_error(pdf_sign_status_t status, std::string_view operation) {
    auto kind = SigningErrorKind::SignatureGenerationFailed;
    if (status == PDF_SIGN_INVALID_ARGUMENT) {
        kind = SigningErrorKind::InvalidArgument;
    } else if (status == PDF_SIGN_CERTIFICATE_ERROR || status == PDF_SIGN_UNAUTHORIZED) {
        kind = SigningErrorKind::KeyError;
    } else if (status == PDF_SIGN_INTERNAL_ERROR) {
        kind = SigningErrorKind::InternalError;
    }
    return {kind, std::string(operation) + " failed: " + std::string(status_name(status))};
}

struct SignerDeleter {
    void operator()(pdf_signer_t* signer) const noexcept {
        pdf_signer_destroy(signer);
    }
};
using SignerHandle = std::unique_ptr<pdf_signer_t, SignerDeleter>;

struct ResultDeleter {
    void operator()(pdf_sign_result_t* result) const noexcept {
        pdf_sign_result_dispose(result);
    }
};

class NativePdfSigner final : public PdfSigner {
  public:
    explicit NativePdfSigner(SignerHandle handle) : handle_(std::move(handle)) {}

    std::expected<std::string, SigningError> sign_digest(std::span<const uint8_t, 32> digest, const SigningContext&,
                                                         double* call_seconds) override {
        const pdf_sign_request_t request{
            .digest_algorithm = PDF_SIGN_DIGEST_SHA256,
            .digest = digest.data(),
            .digest_len = digest.size(),
        };
        pdf_sign_result_t result{};
        // This guard disposes C-owned contents on success, failure and exceptions.
        const std::unique_ptr<pdf_sign_result_t, ResultDeleter> result_guard(&result);
        using Clock = std::chrono::steady_clock;
        const auto start = call_seconds ? Clock::now() : Clock::time_point{};
        const auto status = pdf_signer_sign(handle_.get(), &request, &result);
        if (call_seconds) {
            *call_seconds += std::chrono::duration<double>(Clock::now() - start).count();
        }
        if (status != PDF_SIGN_OK) {
            return std::unexpected(signer_error(status, "pdf_signer_sign"));
        }
        if (result.contents_hex == nullptr || result.contents_hex_len == 0) {
            return std::unexpected(
                SigningError{SigningErrorKind::InvalidSignatureOutput, "C signer returned empty CMS hex"});
        }
        return std::string(result.contents_hex, result.contents_hex_len);
    }

  private:
    SignerHandle handle_;
};

} // namespace

std::expected<std::unique_ptr<PdfSigner>, SigningError> create_native_pdf_signer() {
    pdf_signer_t* raw = nullptr;
    const auto status = pdf_signer_create(&raw);
    SignerHandle handle(raw);
    if (status != PDF_SIGN_OK) {
        return std::unexpected(signer_error(status, "pdf_signer_create"));
    }
    if (!handle) {
        return std::unexpected(SigningError{SigningErrorKind::InternalError, "C signer returned a null handle"});
    }
    return std::make_unique<NativePdfSigner>(std::move(handle));
}

} // namespace nordiska
