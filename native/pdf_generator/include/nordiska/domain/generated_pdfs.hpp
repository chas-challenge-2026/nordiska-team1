#pragma once

#include <cstdint>
#include <string>
#include <vector>

namespace nordiska {

struct PdfDocument {
    std::string document_id;
    std::vector<uint8_t> pdf_bytes;
};

struct GeneratedPdfs {
    uint64_t customer_id{0};
    std::vector<PdfDocument> documents;
};

} // namespace nordiska
