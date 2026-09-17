#pragma once

#include <array>
#include <cstddef>
#include <cstdint>
#include <string>
#include <vector>

namespace nordiska {

// Production BankID / Signicat PAdES slot size (extracted from real Nordiska contract)
inline constexpr size_t kDefaultSignatureSlotSize = 105032; // 105,032 hex characters (52,516 bytes)

// Total capacity needed when appending the signature incremental update block
// (includes widget annotation, /Sig dictionary, ByteRange, 105,032-char placeholder, xref, trailer)
inline constexpr size_t kSignatureBlockSize = 115000;

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
