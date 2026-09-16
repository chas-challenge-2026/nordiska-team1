#include "nordiska/delivery/c_api/pdf_generator_c_api.h"

#include "nordiska/application/generate_customer_batch.hpp"
#include "nordiska/composition/default_composition.hpp"

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

std::expected<void, nordiska_pdf_status> validate_boundary_arguments(const uint8_t* json_utf8, size_t json_length,
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
        set_last_error("Payload size exceeds maximum allowed limit of 32MB");
        return std::unexpected(NORDISKA_PDF_RESOURCE_LIMIT_EXCEEDED);
    }
    return {};
}

} // namespace

extern "C" int nordiska_pdf_v1_generate_customer_batch(const uint8_t* json_utf8, size_t json_length,
                                                       nordiska_pdf_delivery_callback callback, void* user_data) {
    clear_last_error();

    try {
        // 1. Validate ABI boundary arguments
        auto val_res = validate_boundary_arguments(json_utf8, json_length, callback);
        if (!val_res) {
            return val_res.error();
        }

        // 2. Delegate to application batch generation pipeline.
        // The application coordinator owns:
        //   - Swappable JSON input adapter (nlohmann / simdjson / custom) -> owning validated CustomerBatch
        //   - Calling-thread sequential layout & rendering (Libharu / Cairo / custom)
        //   - Optional PDF signing
        //   - Retained complete in-memory BatchResult
        const std::span<const uint8_t> payload_span{json_utf8, json_length};
        auto generator = nordiska::make_default_batch_generator();
        auto batch_result = generator->generate(payload_span);
        if (!batch_result) {
            set_last_error(batch_result.error().message);
            return batch_result.error().status;
        }

        const auto& completed_batch = *batch_result;

        // 3. Construct borrowed C ABI batch and document views
        std::vector<nordiska_pdf_document_view> document_views;
        document_views.reserve(completed_batch.documents.size());
        for (const auto& doc : completed_batch.documents) {
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

        // 4. Synchronously invoke host delivery callback exactly once on calling thread
        const int cb_status = callback(&batch_view, user_data);
        if (cb_status != 0) {
            set_last_error("customer batch callback rejected batch with status code: " + std::to_string(cb_status));
            return NORDISKA_PDF_CALLBACK_FAILED;
        }

        // 5. Success! Returning automatically deallocates completed_batch and views via RAII.
        return NORDISKA_PDF_OK;

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
    default:
        return "status code does not exist";
    }
}
