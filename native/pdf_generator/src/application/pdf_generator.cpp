#include "nordiska/application/pdf_generator.hpp"

#include "nordiska/domain/generated_pdfs.hpp"
#include "nordiska/domain/pdf_rendering_job.hpp"
#include "nordiska/layout/layout_builder.hpp"

#include <chrono>
#include <cstdint>
#include <expected>
#include <span>
#include <string>
#include <vector>

namespace nordiska {

PdfGenerator::PdfGenerator(GeneratorConfig config)
    : ingestor_(config.ingestor), engine_(config.engine, config.compression), enable_signing_(config.enable_signing) {}

PdfGenerator::~PdfGenerator() = default;

std::expected<GeneratedPdfs, GeneratorError> PdfGenerator::generate(std::span<const uint8_t> json_utf8,
                                                                    PipelineTiming* timing) const {
    using Clock = std::chrono::steady_clock;

    const auto t_ingest_start = (timing != nullptr) ? Clock::now() : Clock::time_point{};
    auto ingest_res = ingestor_.ingest(json_utf8);
    if (timing != nullptr) {
        const auto t_ingest_end = Clock::now();
        timing->ingest_seconds += std::chrono::duration<double>(t_ingest_end - t_ingest_start).count();
    }

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
        const auto t_layout_start = (timing != nullptr) ? Clock::now() : Clock::time_point{};
        DocumentLayout layout = LayoutBuilder::build(doc);
        if (timing != nullptr) {
            const auto t_layout_end = Clock::now();
            timing->layout_seconds += std::chrono::duration<double>(t_layout_end - t_layout_start).count();
        }

        const auto t_render_start = (timing != nullptr) ? Clock::now() : Clock::time_point{};
        auto render_res = engine_.render(layout);
        if (timing != nullptr) {
            const auto t_render_end = Clock::now();
            timing->render_seconds += std::chrono::duration<double>(t_render_end - t_render_start).count();
        }

        if (!render_res) {
            // All-or-nothing guarantee: stop on first failure and discard accumulated results
            return std::unexpected(GeneratorError{
                .kind = GeneratorErrorKind::InternalError,
                .message = "Failed to render document '" + doc.document_id + "': " + render_res.error().message,
            });
        }

        generated.documents.push_back(PdfDocument{
            .document_id = doc.document_id,
            .pdf_bytes = std::move(*render_res),
        });
    }

    return generated;
}

} // namespace nordiska
