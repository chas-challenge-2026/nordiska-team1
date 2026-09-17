#include "nordiska/application/pdf_generator.hpp"

#include "nordiska/domain/generated_pdfs.hpp"
#include "nordiska/domain/pdf_rendering_job.hpp"
#include "nordiska/layout/layout_builder.hpp"
#include "nordiska/signing/pdf_signer.hpp"
#include "nordiska/signing/signature_slot_appender.hpp"

#include <chrono>
#include <cstdint>
#include <expected>
#include <memory>
#include <span>
#include <string>
#include <utility>
#include <vector>

namespace nordiska {

namespace {

GeneratorError map_ingest_error(const IngestError& err) noexcept {
    const auto kind = (err.kind == IngestErrorKind::InternalError) ? GeneratorErrorKind::InternalError
                                                                   : GeneratorErrorKind::InvalidInput;
    return GeneratorError{
        .kind = kind,
        .message = err.formatted_message(),
    };
}

} // namespace

PdfGenerator::PdfGenerator(GeneratorConfig config)
    : ingestor_(config.ingestor), engine_(config.engine, config.compression),
      signer_(config.custom_signer ? config.custom_signer : std::make_shared<StubPdfSigner>()),
      enable_signing_(config.enable_signing) {}

// Must be defaulted in .cpp (not header) so the compiler can safely destroy Pimpl members.
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
        return std::unexpected(map_ingest_error(ingest_res.error()));
    }

    const PdfRenderingJob& job = *ingest_res;
    GeneratedPdfs generated;
    generated.customer_id = job.customer_id;
    generated.documents.reserve(job.documents.size()); // reserve first so vector ony allocated memory once

    // Build each PDF one by one
    for (const Document& doc : job.documents) {
        const auto t_layout_start = (timing != nullptr) ? Clock::now() : Clock::time_point{};
        // 1. Layout
        DocumentLayout layout = LayoutBuilder::build(doc);

        if (timing != nullptr) {
            const auto t_layout_end = Clock::now();
            timing->layout_seconds += std::chrono::duration<double>(t_layout_end - t_layout_start).count();
        }

        const auto t_render_start = (timing != nullptr) ? Clock::now() : Clock::time_point{};
        // 2. Render
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

        std::vector<uint8_t> final_bytes = std::move(*render_res);

        // 3. Signature Slot Preparation (In-Place Incremental Append)
        // All three backends (Haru, Cairo, Native) guaranteed spare capacity via
        // buffer.reserve(size + kSignatureBlockSize) upon completing visual rendering.
        // Therefore, appending the ISO 32000-compliant /Sig dictionary, ByteRange,
        // and 8 KB placeholder incurs zero buffer reallocations or heap copies.
        SignatureSlot slot = append_signature_slot(final_bytes);

        if (enable_signing_ && signer_ != nullptr) {
            const auto t_sign_start = (timing != nullptr) ? Clock::now() : Clock::time_point{};
            const SigningContext signing_context{
                .document_id = doc.document_id,
                .customer_id = job.customer_id,
            };
            // 4. Sign
            auto sign_res = signer_->sign(final_bytes, signing_context);

            if (timing != nullptr) {
                const auto t_sign_end = Clock::now();
                timing->sign_seconds += std::chrono::duration<double>(t_sign_end - t_sign_start).count();
            }

            if (!sign_res) {
                // All-or-nothing guarantee: stop on first signing failure, discard accumulated results, invoke zero
                // callbacks
                return std::unexpected(GeneratorError{
                    .kind = GeneratorErrorKind::SigningError,
                    .message = "Failed to sign document '" + doc.document_id + "': " + sign_res.error().message,
                });
            }
            final_bytes = std::move(*sign_res);
            slot.is_signed = true;
        }

        generated.documents.push_back(PdfDocument{
            .document_id = doc.document_id,
            .pdf_bytes = std::move(final_bytes),
            .signature_slot = slot,
        });
    }

    return generated;
}

} // namespace nordiska
