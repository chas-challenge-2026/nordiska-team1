#pragma once

#include <array>
#include <cstddef>
#include <cstdint>
#include <string>
#include <vector>

namespace nordiska {

// Capacity is measured in hex characters, excluding the < > delimiters. Callers can select a larger slot.
inline constexpr size_t kDefaultSignatureSlotSize = 8192;
inline constexpr size_t kSignatureUpdateOverhead = 4096;

struct SignatureSlot {
    size_t offset{0};
    size_t max_length{kDefaultSignatureSlotSize};
    bool is_signed{false};
};

struct PdfDocument {
    std::string document_id;
    std::vector<uint8_t> pdf_bytes;
    // Hash of the complete delivered artifact, distinct from the signing digest.
    std::array<uint8_t, 32> sha256_hash{};
    std::array<uint8_t, 32> signing_digest{};
    SignatureSlot signature_slot{};
};

struct GeneratedPdfs {
    uint64_t customer_id{0};
    std::vector<PdfDocument> documents;
};

} // namespace nordiska
