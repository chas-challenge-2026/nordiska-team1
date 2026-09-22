#include "nordiska/c_api/pdf_generator_c_api.h"

#include "nordiska/application/pdf_generator.hpp"

#include <cstddef>
#include <cstdint>
#include <exception>
#include <expected>
#include <new>
#include <span>
#include <string>
#include <string_view>
#include <vector>

namespace {

// Using constexpr int instead of enum guarantees fixed 32-bit int types across the C ABI
// and avoids compiler-dependent enum sizes or type-casting across language boundaries.
constexpr int NORDISKA_PDF_OK = 0;
constexpr int NORDISKA_PDF_INVALID_ARGUMENT = 1;
constexpr int NORDISKA_PDF_INVALID_INPUT = 2;
constexpr int NORDISKA_PDF_CALLBACK_FAILED = 3;
constexpr int NORDISKA_PDF_INTERNAL_ERROR = 4;
constexpr int NORDISKA_PDF_RESOURCE_LIMIT_EXCEEDED = 5;
constexpr int NORDISKA_PDF_OUT_OF_MEMORY = 6;
constexpr int NORDISKA_PDF_SIGNING_FAILED = 7;

constexpr size_t kMaxJsonPayloadBytes = 32 * 1024 * 1024; // 32 MB (NOR-157)

// Thread-local diagnostic storage for error reporting
thread_local std::string g_last_error;
thread_local const char* g_fallback_static_error = nullptr;

void clear_last_error() noexcept {
    g_fallback_static_error = nullptr;
    g_last_error.clear();
}

void set_last_error(std::string_view message) noexcept {
    g_fallback_static_error = nullptr;
    try {
        g_last_error.assign(message.data(), message.size());
    } catch (...) {
        // Non-allocating fallback if recording diagnostic fails under memory pressure
        g_fallback_static_error = "Out of memory while recording error diagnostic";
    }
}

void set_last_error_static(const char* message) noexcept {
    g_fallback_static_error = message;
    g_last_error.clear();
}

std::expected<void, int> validate_boundary_arguments(const uint8_t* json_utf8, size_t json_length,
                                                     nordiska_pdf_delivery_callback callback) noexcept {
    if (json_utf8 == nullptr) {
        set_last_error("json_utf8 must not be null");
        return std::unexpected(NORDISKA_PDF_INVALID_ARGUMENT);
    }
    if (json_length == 0) {
        set_last_error("json_length must be greater than zero");
        return std::unexpected(NORDISKA_PDF_INVALID_ARGUMENT);
    }
    if (callback == nullptr) {
        set_last_error("callback must not be null");
        return std::unexpected(NORDISKA_PDF_INVALID_ARGUMENT);
    }
    if (json_length > kMaxJsonPayloadBytes) {
        set_last_error("Payload size exceeds maximum allowed limit");
        return std::unexpected(NORDISKA_PDF_RESOURCE_LIMIT_EXCEEDED);
    }
    return {};
}

int map_generator_error(const nordiska::GeneratorError& err) noexcept {
    set_last_error(err.message);
    switch (err.kind) {
    case nordiska::GeneratorErrorKind::InvalidArgument:
        return NORDISKA_PDF_INVALID_ARGUMENT;
    case nordiska::GeneratorErrorKind::InvalidInput:
        return NORDISKA_PDF_INVALID_INPUT;
    case nordiska::GeneratorErrorKind::ResourceLimitExceeded:
        return NORDISKA_PDF_RESOURCE_LIMIT_EXCEEDED;
    case nordiska::GeneratorErrorKind::SignaturePreparationFailed:
    case nordiska::GeneratorErrorKind::HashingFailed:
    case nordiska::GeneratorErrorKind::InvalidSignatureOutput:
    case nordiska::GeneratorErrorKind::SignatureTooLarge:
    case nordiska::GeneratorErrorKind::SigningError:
        return NORDISKA_PDF_SIGNING_FAILED;
    case nordiska::GeneratorErrorKind::InternalError:
    default:
        return NORDISKA_PDF_INTERNAL_ERROR;
    }
}

int deliver_batch(const nordiska::GeneratedPdfs& completed_batch, nordiska_pdf_delivery_callback callback,
                  void* user_data) {
    std::vector<nordiska_pdf_document_view> document_views;
    document_views.reserve(completed_batch.documents.size());

    for (const nordiska::PdfDocument& doc : completed_batch.documents) {
        document_views.push_back(nordiska_pdf_document_view{
            .document_id = doc.document_id.c_str(),
            .bytes = doc.pdf_bytes.data(),
            .length = doc.pdf_bytes.size(),
        });
    }

    const nordiska_pdf_batch_view batch_view{
        .customer_id = completed_batch.customer_id,
        .documents = document_views.data(),
        .document_count = document_views.size(),
    };

    const int cb_status = callback(&batch_view, user_data);
    if (cb_status != 0) {
        set_last_error("customer batch callback rejected batch with status code: " + std::to_string(cb_status));
        return NORDISKA_PDF_CALLBACK_FAILED;
    }

    return NORDISKA_PDF_OK;
}

} // namespace

extern "C" int nordiska_pdf_v1_generate_customer_batch(const uint8_t* json_utf8, size_t json_length,
                                                       nordiska_pdf_delivery_callback callback, void* user_data) {
    clear_last_error(); // clear thread local error before we start

    try {
        auto val_res = validate_boundary_arguments(json_utf8, json_length, callback);
        if (!val_res) {
            return val_res.error();
        }

        const std::span<const uint8_t> payload_span{json_utf8, json_length};

        const nordiska::GeneratorConfig config{
            .ingestor = nordiska::JsonIngestorKind::Simdjson,
            .engine = nordiska::PdfEngineKind::Libharu, // TODO native is faster (and smaller) but must be tested more
            .enable_signing = false,
            .compression = true, // Cut output size by 60% for a 25% performance penalty, huge win
        };

        // Generate PDFs
        nordiska::PdfGenerator generator(config);
        auto batch_result = generator.generate(payload_span);
        if (!batch_result) {
            return map_generator_error(batch_result.error());
        }

        return deliver_batch(*batch_result, callback, user_data);

    } catch (const std::bad_alloc&) {
        set_last_error_static("Out of memory during document generation");
        return NORDISKA_PDF_OUT_OF_MEMORY;
    } catch (const std::exception& err) {
        set_last_error(err.what());
        return NORDISKA_PDF_INTERNAL_ERROR;
    } catch (...) {
        set_last_error_static("Unknown native document batch generation error");
        return NORDISKA_PDF_INTERNAL_ERROR;
    }
}

extern "C" size_t nordiska_pdf_v1_max_json_bytes(void) {
    return kMaxJsonPayloadBytes;
}

extern "C" const char* nordiska_pdf_v1_get_last_error(void) {
    if (g_fallback_static_error != nullptr) {
        return g_fallback_static_error;
    }
    return g_last_error.c_str();
}

extern "C" const char* nordiska_pdf_v1_status_name(int status) {
    switch (status) {
    case NORDISKA_PDF_OK:
        return "NORDISKA_PDF_OK";
    case NORDISKA_PDF_INVALID_ARGUMENT:
        return "NORDISKA_PDF_INVALID_ARGUMENT";
    case NORDISKA_PDF_INVALID_INPUT:
        return "NORDISKA_PDF_INVALID_INPUT";
    case NORDISKA_PDF_CALLBACK_FAILED:
        return "NORDISKA_PDF_CALLBACK_FAILED";
    case NORDISKA_PDF_INTERNAL_ERROR:
        return "NORDISKA_PDF_INTERNAL_ERROR";
    case NORDISKA_PDF_RESOURCE_LIMIT_EXCEEDED:
        return "NORDISKA_PDF_RESOURCE_LIMIT_EXCEEDED";
    case NORDISKA_PDF_OUT_OF_MEMORY:
        return "NORDISKA_PDF_OUT_OF_MEMORY";
    case NORDISKA_PDF_SIGNING_FAILED:
        return "NORDISKA_PDF_SIGNING_FAILED";
    default:
        return "status code does not exist";
    }
}
