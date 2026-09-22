#pragma once

#include "nordiska/domain/generated_pdfs.hpp"
#include "nordiska/signing/pdf_signer.hpp"

#include <span>
#include <string_view>
#include <vector>

namespace nordiska {

// Only fresh PDFs produced by our renderers are supported (classic xref, no forms,
// encryption or previous revisions). Unsupported structures fail before mutation.
[[nodiscard]] std::expected<SignatureSlot, SigningError>
append_signature_slot(std::vector<uint8_t>& pdf_buffer, size_t contents_hex_capacity = kDefaultSignatureSlotSize);

[[nodiscard]] std::expected<Sha256Digest, SigningError> calculate_signing_digest(std::span<const uint8_t> pdf,
                                                                                 const SignatureSlot& slot);

[[nodiscard]] std::expected<Sha256Digest, SigningError> calculate_pdf_hash(std::span<const uint8_t> pdf);

// Validates the entire result before touching the PDF. Never changes its length.
[[nodiscard]] std::expected<void, SigningError> insert_signature(std::span<uint8_t> pdf, SignatureSlot& slot,
                                                                 std::string_view contents_hex);

} // namespace nordiska
