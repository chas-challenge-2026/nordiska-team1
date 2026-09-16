#include "nordiska/application/pdf_generator.hpp"

#include "nordiska/domain/generated_pdfs.hpp"
#include "nordiska/domain/pdf_rendering_job.hpp"

#include <cstdint>
#include <expected>
#include <span>
#include <string>
#include <vector>

namespace nordiska {

PdfGenerator::PdfGenerator(GeneratorConfig config)
    : ingestor_(config.ingestor), engine_(config.engine), enable_signing_(config.enable_signing) {}

PdfGenerator::~PdfGenerator() = default;

std::expected<GeneratedPdfs, GeneratorError> PdfGenerator::generate(std::span<const uint8_t> json_utf8) const {
    auto ingest_res = ingestor_.ingest(json_utf8);
    if (!ingest_res) {
        GeneratorErrorKind kind = GeneratorErrorKind::InvalidInput;
        if (ingest_res.error().kind == IngestErrorKind::InternalError) {
            kind = GeneratorErrorKind::InternalError;
        }
        return std::unexpected(GeneratorError{
            .kind = kind,
            .message = ingest_res.error().formatted_message(),
        });
    }

    const PdfRenderingJob& job = *ingest_res;
    GeneratedPdfs generated;
    generated.customer_id = job.customer_id;
    generated.documents.reserve(job.documents.size());

    for (const Document& doc : job.documents) {
        // Temporary PDF header stub until Step 3/4/5 layout and rendering are wired
        static const uint8_t kStubPdf[] = "%PDF-1.3\n%fake\n%%EOF";
        generated.documents.push_back(PdfDocument{
            .document_id = doc.document_id,
            .pdf_bytes = std::vector<uint8_t>(std::begin(kStubPdf), std::end(kStubPdf) - 1),
        });
    }

    return generated;
}

} // namespace nordiska
