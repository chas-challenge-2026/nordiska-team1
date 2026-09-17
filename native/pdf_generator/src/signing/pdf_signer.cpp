#include "nordiska/signing/pdf_signer.hpp"

#include <cstdint>
#include <expected>
#include <span>
#include <string>
#include <vector>

namespace nordiska {

std::expected<std::vector<uint8_t>, SigningError> StubPdfSigner::sign(std::span<const uint8_t> unsigned_pdf,
                                                                      const SigningContext& context) {
    if (simulate_failure_) {
        if (failure_document_id_.empty() || context.document_id == failure_document_id_) {
            return std::unexpected(SigningError{
                .kind = SigningErrorKind::SignatureGenerationFailed,
                .message = "Simulated signing failure for document '" + std::string(context.document_id) + "'",
            });
        }
    }

    // Pass-through stub: returns unmutated bytes until native/pdf-signer
    // provides detached PKCS#7 signature injection into the PDF object stream.
    return std::vector<uint8_t>(unsigned_pdf.begin(), unsigned_pdf.end());
}

} // namespace nordiska
