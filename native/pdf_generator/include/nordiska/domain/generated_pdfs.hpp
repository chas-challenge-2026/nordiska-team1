#pragma once

#include <array>
#include <cstddef>
#include <cstdint>
#include <string>
#include <vector>

namespace nordiska {

// Standard 8 KB placeholder for CMS DER hex encoding (certificate chain + timestamp)
inline constexpr size_t kDefaultSignatureSlotSize = 8192;

// Total capacity needed when appending the signature incremental update block
// (includes widget annotation, /Sig dictionary, ByteRange, 8192-char placeholder, xref, trailer)
inline constexpr size_t kSignatureBlockSize = 10240;

struct SignatureSlot {
    size_t offset{0};
    size_t max_length{kDefaultSignatureSlotSize};
    bool is_signed{false};
};

struct PdfDocument {
    std::string document_id;
    std::vector<uint8_t> pdf_bytes;
    std::array<uint8_t, 32> sha256_hash{};
    SignatureSlot signature_slot{};
};

struct GeneratedPdfs {
    uint64_t customer_id{0};
    std::vector<PdfDocument> documents;
};

} // namespace nordiska
