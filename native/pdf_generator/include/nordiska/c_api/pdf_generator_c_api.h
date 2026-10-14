#ifndef NORDISKA_C_API_PDF_GENERATOR_C_API_H
#define NORDISKA_C_API_PDF_GENERATOR_C_API_H

/*
 * Nordiska Native PDF Generator — C ABI
 *
 * Shared contract for V1 and V2
 * -----------------------------
 * Each synchronous, stateless call generates one customer's complete PDF batch
 * on the calling thread. Configuration and generator components are resolved
 * per call. Concurrent calls for separate batches are independent.
 *
 * Input: json_utf8 must be non-NULL and json_length must be 1..33,554,432 bytes
 * (32 MB). The caller must keep this UTF-8 JSON buffer valid and unmodified until
 * generation returns. No caller input pointers are retained afterward. See
 * docs/golden_customer_batch_sample.json for the canonical input schema.
 * Banking values are supplied pre-calculated and formatted by the host.
 *
 * Delivery: callback must be non-NULL. It is invoked exactly once after every
 * document succeeds. Validation, layout, rendering, signing or limit failures
 * discard the batch and invoke no callback. user_data is forwarded unchanged
 * and may be NULL. Return 0 from the callback to accept the batch; any other
 * return value makes generation return status 3 (callback failed).
 *
 * Ownership: all batch views, identifiers, PDF bytes and V2 hash strings belong
 * to native code and are borrowed ONLY for the callback's duration. Copy them
 * or finish using them before returning. The caller must not free these buffers.
 *
 * Configuration: NORDISKA_PDF_ENGINE accepts native, libharu (or haru), and
 * cairo, case-insensitively. Unset or empty selects Libharu. An unknown value
 * returns status 1 (invalid argument) without invoking the callback.
 *
 * Results: generation returns 0 on success and a non-zero status on failure.
 * Both versions use the shared helpers below for status names, payload limits
 * and thread-local diagnostics. Error strings remain valid until the next
 * generation call on the same thread; successful calls leave an empty string.
 *
 * Boundary: C-compatible primitive types, structs and function pointers only.
 * No C++ classes, STL types or exceptions cross the ABI.
 */

#include <stddef.h>
#include <stdint.h>

#define NORDISKA_PDF_API __attribute__((visibility("default")))

#ifdef __cplusplus
extern "C" {
#endif

/* ========================================================================== */
/* V1 — PDF delivery                                                          */
/* ========================================================================== */

struct nordiska_pdf_document_view {
    const char* document_id;
    const uint8_t* bytes;
    size_t length;
};

struct nordiska_pdf_batch_view {
    uint64_t customer_id;
    const struct nordiska_pdf_document_view* documents;
    size_t document_count;
};

typedef int (*nordiska_pdf_delivery_callback)(const struct nordiska_pdf_batch_view* batch, void* user_data);

/* Generate and deliver PDF bytes under the shared contract above. */
NORDISKA_PDF_API int nordiska_pdf_v1_generate_customer_batch(const uint8_t* json_utf8, size_t json_length,
                                                             nordiska_pdf_delivery_callback callback, void* user_data);

/* ========================================================================== */
/* V2 — PDF delivery with artifact hashes                                     */
/* ========================================================================== */

/* Separate V2 views preserve the original V1 struct layouts and callback ABI. */
struct nordiska_pdf_document_view_v2 {
    const char* document_id;
    const uint8_t* bytes;
    size_t length;
    /* Native-computed SHA-256 of the complete delivered PDF, including any
     * inserted signature: 64 lowercase hex characters plus a NUL terminator.
     * Borrowed only during the callback, just like bytes and document_id. */
    const char* sha256_hex;
};

struct nordiska_pdf_batch_view_v2 {
    uint64_t customer_id;
    const struct nordiska_pdf_document_view_v2* documents;
    size_t document_count;
};

typedef int (*nordiska_pdf_delivery_callback_v2)(const struct nordiska_pdf_batch_view_v2* batch, void* user_data);

/* Generate and deliver PDF bytes and hashes under the same shared contract. */
NORDISKA_PDF_API int nordiska_pdf_v2_generate_customer_batch(const uint8_t* json_utf8, size_t json_length,
                                                             nordiska_pdf_delivery_callback_v2 callback,
                                                             void* user_data);

/* ========================================================================== */
/* Shared helpers — V1 names retained for compatibility; used by both versions */
/* ========================================================================== */

/* Maximum input length in bytes, for host admission control. */
NORDISKA_PDF_API size_t nordiska_pdf_v1_max_json_bytes(void);

/* Diagnostic for the last generation call on this thread; empty on success.
 * Never NULL. Borrowed until the next generation call; do not free or mutate. */
NORDISKA_PDF_API const char* nordiska_pdf_v1_get_last_error(void);

/* Human-readable status name, or "status code does not exist". Never NULL. */
NORDISKA_PDF_API const char* nordiska_pdf_v1_status_name(int status);

#ifdef __cplusplus
}
#endif

#endif /* NORDISKA_C_API_PDF_GENERATOR_C_API_H */
