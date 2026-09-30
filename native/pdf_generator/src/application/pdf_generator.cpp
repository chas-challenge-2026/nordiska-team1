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
#include <type_traits>
#include <utility>
#include <variant>
#include <vector>

namespace nordiska {

std::string GeneratorError::message() const {
    return std::visit(
        [this](const auto& err) -> std::string {
            using T = std::decay_t<decltype(err)>;
            if constexpr (std::is_same_v<T, IngestError>) {
                return err.formatted_message();
            } else if constexpr (std::is_same_v<T, LayoutError>) {
                if (!document_id.empty()) {
                    return "Failed to layout document '" + document_id + "': " + err.message;
                }
                return err.message;
            } else if constexpr (std::is_same_v<T, RenderError>) {
                if (!document_id.empty()) {
                    return "Failed to render document '" + document_id + "': " + err.message;
                }
                return err.message;
            } else if constexpr (std::is_same_v<T, SigningError>) {
                if (!document_id.empty()) {
                    return "Failed to sign document '" + document_id + "': " + err.message;
                }
                return err.message;
            } else {
                return err.message;
            }
        },
        details);
}

PdfGenerator::PdfGenerator(GeneratorConfig config)
    : ingestor_(config.ingestor), engine_(config.engine, config.compression), signer_(std::move(config.custom_signer)),
      enable_signing_(config.enable_signing), signature_contents_capacity_(config.signature_contents_capacity) {
    if (enable_signing_ && !signer_) {
        auto signer = create_native_pdf_signer();
        if (signer) {
            signer_ = std::move(*signer);
        } else {
            signer_initialization_error_ = std::move(signer.error());
        }
    }
}

// Must be defaulted in .cpp (not header) so the compiler can safely destroy Pimpl members.
PdfGenerator::~PdfGenerator() = default;

std::expected<GeneratedPdfs, GeneratorError> PdfGenerator::generate(std::span<const uint8_t> json_utf8,
                                                                    PipelineTiming* timing) const {
    // Classic PDF xref entries have 10 decimal digits for byte offsets.
    if (signature_contents_capacity_ == 0 || signature_contents_capacity_ % 2 != 0 ||
        signature_contents_capacity_ > 9'999'999'999ULL - kSignatureUpdateOverhead) {
        return std::unexpected(GeneratorError{
            InvalidArgumentError{"Signature capacity must be a positive even hex length within PDF offset limits"}});
    }
    if (signer_initialization_error_) {
        return std::unexpected(GeneratorError{*signer_initialization_error_});
    }
    using Clock = std::chrono::steady_clock;
    const auto t_ingest_start = (timing != nullptr) ? Clock::now() : Clock::time_point{};

    auto ingest_res = ingestor_.ingest(json_utf8);

    if (timing != nullptr) {
        const auto t_ingest_end = Clock::now();
        timing->ingest_seconds += std::chrono::duration<double>(t_ingest_end - t_ingest_start).count();
    }

    if (!ingest_res) {
        return std::unexpected(GeneratorError{std::move(ingest_res).error()});
    }

    const PdfRenderingJob& job = *ingest_res;
    GeneratedPdfs generated;
    generated.customer_id = job.customer_id;
    generated.documents.reserve(job.documents.size()); // reserve first so vector ony allocated memory once

    // Build each PDF one by one
    for (const Document& doc : job.documents) {
        const auto t_layout_start = (timing != nullptr) ? Clock::now() : Clock::time_point{};
        // 1. Layout
        auto layout_res = LayoutBuilder::build(doc);
        if (!layout_res) {
            return std::unexpected(GeneratorError{std::move(layout_res).error(), doc.document_id});
        }

        if (timing != nullptr) {
            const auto t_layout_end = Clock::now();
            timing->layout_seconds += std::chrono::duration<double>(t_layout_end - t_layout_start).count();
        }

        DocumentLayout layout = std::move(*layout_res);

        const auto t_render_start = (timing != nullptr) ? Clock::now() : Clock::time_point{};
        // 2. Render
        auto render_res = engine_.render(layout, signature_contents_capacity_ + kSignatureUpdateOverhead);

        if (timing != nullptr) {
            const auto t_render_end = Clock::now();
            timing->render_seconds += std::chrono::duration<double>(t_render_end - t_render_start).count();
        }

        if (!render_res) {
            // All-or-nothing guarantee: stop on first failure and discard accumulated results
            return std::unexpected(GeneratorError{std::move(render_res).error(), doc.document_id});
        }

        std::vector<uint8_t> final_bytes = std::move(*render_res);

        const auto t_sign_start = (timing != nullptr) ? Clock::now() : Clock::time_point{};
        auto slot_result =
            append_signature_slot(final_bytes, signature_contents_capacity_, timing ? &timing->preparation : nullptr);
        if (timing) {
            timing->prepare_seconds += std::chrono::duration<double>(Clock::now() - t_sign_start).count();
        }
        if (!slot_result) {
            return std::unexpected(GeneratorError{std::move(slot_result).error(), doc.document_id});
        }
        SignatureSlot slot = *slot_result;
        const auto t_hash_start = (timing != nullptr) ? Clock::now() : Clock::time_point{};
        auto digest = compute_byte_range_digest(final_bytes, slot);
        if (timing != nullptr) {
            const auto seconds = std::chrono::duration<double>(Clock::now() - t_hash_start).count();
            timing->digest_seconds += seconds;
            timing->hash_seconds += seconds;
        }
        if (!digest) {
            return std::unexpected(GeneratorError{std::move(digest).error(), doc.document_id});
        }
        if (enable_signing_) {
            const SigningContext signing_context{.document_id = doc.document_id, .customer_id = job.customer_id};
            const auto signer_start = timing ? Clock::now() : Clock::time_point{};
            auto signature =
                signer_->sign_digest(*digest, signing_context, timing ? &timing->signer_call_seconds : nullptr);
            if (timing) {
                timing->signer_wrapper_seconds += std::chrono::duration<double>(Clock::now() - signer_start).count();
            }
            if (!signature) {
                return std::unexpected(GeneratorError{std::move(signature).error(), doc.document_id});
            }
            const auto insert_start = timing ? Clock::now() : Clock::time_point{};
            auto inserted = insert_signature(final_bytes, slot, *signature);
            if (timing) {
                timing->insert_seconds += std::chrono::duration<double>(Clock::now() - insert_start).count();
            }
            if (!inserted) {
                return std::unexpected(GeneratorError{std::move(inserted).error(), doc.document_id});
            }
        }
        const auto t_checksum_start = (timing != nullptr) ? Clock::now() : Clock::time_point{};
        auto artifact_hash = hash_final_document(final_bytes);
        if (timing != nullptr) {
            const auto seconds = std::chrono::duration<double>(Clock::now() - t_checksum_start).count();
            timing->checksum_seconds += seconds;
            timing->hash_seconds += seconds;
        }
        if (!artifact_hash) {
            return std::unexpected(GeneratorError{std::move(artifact_hash).error(), doc.document_id});
        }
        if (timing != nullptr) {
            timing->sign_seconds += std::chrono::duration<double>(Clock::now() - t_sign_start).count();
        }

        generated.documents.push_back(PdfDocument{
            .document_id = doc.document_id,
            .pdf_bytes = std::move(final_bytes),
            .sha256_hash = *artifact_hash,
            .signing_digest = *digest,
            .signature_slot = slot,
        });
    }

    return generated;
}

} // namespace nordiska
