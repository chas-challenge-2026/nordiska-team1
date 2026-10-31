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
#include <new>
#include <span>
#include <string>
#include <type_traits>
#include <utility>
#include <variant>
#include <vector>

namespace nordiska {

Diagnostic GeneratorError::diagnostic() const noexcept {
    auto result = std::visit(
        [](const auto& error) {
            using T = std::decay_t<decltype(error)>;
            Diagnostic diagnostic{.code = error.code};
            if constexpr (std::is_same_v<T, IngestError>) {
                diagnostic.stage = DiagnosticStage::Ingestion;
            } else if constexpr (std::is_same_v<T, LayoutError>) {
                diagnostic.stage = DiagnosticStage::Layout;
            } else if constexpr (std::is_same_v<T, RenderError>) {
                diagnostic.stage = DiagnosticStage::Rendering;
            } else if constexpr (std::is_same_v<T, SigningError>) {
                diagnostic.stage = DiagnosticStage::Signing;
            }
            if constexpr (requires { error.location; }) {
                diagnostic.location = error.location;
            }
            return diagnostic;
        },
        details);
    if (document_index) {
        result.location.document_index = document_index;
    }
    return result;
}

std::string GeneratorError::message() const {
    return std::string(format_diagnostic(diagnostic()).view());
}

PdfGenerator::PdfGenerator(GeneratorConfig config)
    : ingestor_(config.ingestor), engine_(config.engine, config.compression), signer_(std::move(config.custom_signer)),
      enable_signing_(config.enable_signing), signature_contents_capacity_(config.signature_contents_capacity),
      report_profile_(config.report_profile) {
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

namespace {

struct SignedDocumentArtifacts {
    SignatureSlot slot{.max_length = 0};
    Sha256Digest digest{};
};

std::expected<SignedDocumentArtifacts, SigningError> sign_rendered_document(std::vector<uint8_t>& pdf_bytes,
                                                                            size_t signature_capacity,
                                                                            PdfSigner& signer, PipelineTiming* timing) {
    using Clock = std::chrono::steady_clock;

    // 1. Append incremental PDF 1.7 signature slot and /ByteRange
    const auto t_prep_start = (timing != nullptr) ? Clock::now() : Clock::time_point{};
    auto slot_result = append_signature_slot(pdf_bytes, signature_capacity, timing ? &timing->preparation : nullptr);
    if (timing != nullptr) {
        timing->prepare_seconds += std::chrono::duration<double>(Clock::now() - t_prep_start).count();
    }
    if (!slot_result) {
        return std::unexpected(std::move(slot_result).error());
    }
    SignatureSlot slot = *slot_result;

    // 2. Compute SHA-256 digest over the two signed byte ranges
    const auto t_hash_start = (timing != nullptr) ? Clock::now() : Clock::time_point{};
    auto digest_result = compute_byte_range_digest(pdf_bytes, slot);
    if (timing != nullptr) {
        const auto seconds = std::chrono::duration<double>(Clock::now() - t_hash_start).count();
        timing->digest_seconds += seconds;
        timing->hash_seconds += seconds;
    }
    if (!digest_result) {
        return std::unexpected(std::move(digest_result).error());
    }

    // 3. Request detached CMS signature from external signer
    const auto signer_start = (timing != nullptr) ? Clock::now() : Clock::time_point{};
    auto signature = signer.sign_digest(*digest_result, timing ? &timing->signer_call_seconds : nullptr);
    if (timing != nullptr) {
        timing->signer_wrapper_seconds += std::chrono::duration<double>(Clock::now() - signer_start).count();
    }
    if (!signature) {
        return std::unexpected(std::move(signature).error());
    }

    // 4. Embed CMS hex into reserved /Contents slot
    const auto insert_start = (timing != nullptr) ? Clock::now() : Clock::time_point{};
    auto inserted = insert_signature(pdf_bytes, slot, *signature);
    if (timing != nullptr) {
        timing->insert_seconds += std::chrono::duration<double>(Clock::now() - insert_start).count();
    }
    if (!inserted) {
        return std::unexpected(std::move(inserted).error());
    }

    return SignedDocumentArtifacts{
        .slot = slot,
        .digest = *digest_result,
    };
}

} // namespace

std::expected<GeneratedPdfs, GeneratorError> PdfGenerator::generate(std::span<const uint8_t> json_utf8,
                                                                    PipelineTiming* timing) const try {
    // Classic PDF xref entries have 10 decimal digits for byte offsets.
    if (signature_contents_capacity_ == 0 || signature_contents_capacity_ % 2 != 0 ||
        signature_contents_capacity_ > 9'999'999'999ULL - kSignatureUpdateOverhead) {
        return std::unexpected(GeneratorError{InvalidArgumentError{DiagnosticCode::InvalidSignatureCapacity}});
    }
    if (signer_initialization_error_) {
        return std::unexpected(GeneratorError{*signer_initialization_error_});
    }
    using Clock = std::chrono::steady_clock;

    // Stage 1: Ingest UTF-8 JSON batch into domain model
    const auto t_ingest_start = (timing != nullptr) ? Clock::now() : Clock::time_point{};
    auto ingest_res = ingestor_.ingest(json_utf8);
    if (timing != nullptr) {
        timing->ingest_seconds += std::chrono::duration<double>(Clock::now() - t_ingest_start).count();
    }
    if (!ingest_res) {
        return std::unexpected(GeneratorError{std::move(ingest_res).error()});
    }

    const PdfRenderingJob& job = *ingest_res;
    GeneratedPdfs generated;
    generated.customer_id = job.customer_id;
    generated.documents.reserve(job.documents.size());

    // Build each PDF sequentially
    for (size_t index = 0; index < job.documents.size(); ++index) {
        const Document& doc = job.documents[index];
        // Stage 2: Layout document geometry and pagination
        const auto t_layout_start = (timing != nullptr) ? Clock::now() : Clock::time_point{};
        auto layout_res =
            LayoutBuilder::build(doc, {.customer_name = job.customer_name, .report_profile = report_profile_});
        if (!layout_res) {
            return std::unexpected(GeneratorError{std::move(layout_res).error(), index});
        }
        if (timing != nullptr) {
            timing->layout_seconds += std::chrono::duration<double>(Clock::now() - t_layout_start).count();
        }
        DocumentLayout layout = std::move(*layout_res);

        // Stage 3: Render binary PDF 1.4 stream
        const auto t_render_start = (timing != nullptr) ? Clock::now() : Clock::time_point{};
        const size_t signature_tail = enable_signing_ ? signature_contents_capacity_ + kSignatureUpdateOverhead : 0;
        auto render_res = engine_.render(layout, signature_tail);
        if (timing != nullptr) {
            timing->render_seconds += std::chrono::duration<double>(Clock::now() - t_render_start).count();
        }
        if (!render_res) {
            // All-or-nothing guarantee: stop on first failure and discard accumulated results
            return std::unexpected(GeneratorError{std::move(render_res).error(), index});
        }
        std::vector<uint8_t> final_bytes = std::move(*render_res);

        // Stage 4: Optional digital signing + Final artifact SHA-256 checksum
        const auto t_sign_start = (timing != nullptr) ? Clock::now() : Clock::time_point{};
        SignedDocumentArtifacts signed_artifacts{};
        if (enable_signing_) {
            auto sign_res = sign_rendered_document(final_bytes, signature_contents_capacity_, *signer_, timing);
            if (!sign_res) {
                return std::unexpected(GeneratorError{std::move(sign_res).error(), index});
            }
            signed_artifacts = *sign_res;
        }

        const auto t_checksum_start = (timing != nullptr) ? Clock::now() : Clock::time_point{};
        auto artifact_hash = hash_final_document(final_bytes);
        if (timing != nullptr) {
            const auto seconds = std::chrono::duration<double>(Clock::now() - t_checksum_start).count();
            timing->checksum_seconds += seconds;
            timing->hash_seconds += seconds;
        }
        if (!artifact_hash) {
            return std::unexpected(GeneratorError{std::move(artifact_hash).error(), index});
        }
        if (timing != nullptr) {
            timing->sign_seconds += std::chrono::duration<double>(Clock::now() - t_sign_start).count();
        }

        generated.documents.push_back(PdfDocument{
            .document_id = doc.document_id,
            .pdf_bytes = std::move(final_bytes),
            .sha256_hash = *artifact_hash,
            .signing_digest = signed_artifacts.digest,
            .signature_slot = signed_artifacts.slot,
        });
    }

    return generated;
} catch (const std::bad_alloc&) {
    return std::unexpected(GeneratorError{InternalError{DiagnosticCode::OutOfMemory}});
} catch (...) {
    return std::unexpected(GeneratorError{InternalError{}});
}

} // namespace nordiska
