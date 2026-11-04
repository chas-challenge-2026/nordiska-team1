#include "json_to_pdf.hpp"

#include "nordiska/application/pdf_generator.hpp"

#include <algorithm>
#include <cstdio>
#include <cstdlib>
#include <string_view>
#include <unordered_set>

namespace nordiska::fuzz {
namespace {

void require(bool condition, const char* message) {
    if (!condition) {
        std::fprintf(stderr, "JSON-to-PDF contract failure: %s\n", message);
        std::abort();
    }
}

} // namespace

bool json_to_pdf(std::span<const std::uint8_t> input) {
    if (input.size() > kMaxInputBytes) {
        return false;
    }

    // Explicit configuration: environment variables cannot enable signing here.
    // Per-input ownership also exercises construction/destruction during replay.
    GeneratorConfig config;
    config.ingestor = JsonIngestorKind::Simdjson;
    config.engine = PdfEngineKind::Libharu;
    config.enable_signing = false;
    config.compression = true;
    PdfGenerator generator(config);
    auto result = generator.generate(input);
    if (!result) {
        return false;
    }

    require(result->customer_id > 0, "successful batch has no customer");
    require(!result->documents.empty(), "successful batch is empty");
    std::unordered_set<std::string_view> ids;
    for (const auto& document : result->documents) {
        require(!document.document_id.empty(), "document ID is empty");
        require(ids.insert(document.document_id).second, "duplicate document ID");
        const auto& bytes = document.pdf_bytes;
        require(!bytes.empty(), "successful PDF is empty");
        std::string_view pdf(reinterpret_cast<const char*>(bytes.data()), bytes.size());
        require(pdf.starts_with("%PDF-"), "missing PDF header");
        const auto end = pdf.find_last_not_of("\r\n\t ");
        require(end != std::string_view::npos && pdf.substr(0, end + 1).ends_with("%%EOF"), "missing PDF EOF");
        require(!document.signature_slot.is_signed, "unsigned target signed a document");
        require(std::all_of(document.signing_digest.begin(), document.signing_digest.end(),
                            [](auto byte) { return byte == 0; }),
                "unsigned target computed a signing digest");
    }
    return true;
}

} // namespace nordiska::fuzz
