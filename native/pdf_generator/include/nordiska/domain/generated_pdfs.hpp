#pragma once

#include <array>
#include <cstddef>
#include <cstdint>
#include <string>
#include <vector>

namespace nordiska {

struct SignatureSlot {
    size_t offset{0};
    size_t max_length{0};
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

