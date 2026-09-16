#ifndef NORDISKA_DELIVERY_C_API_PDF_GENERATOR_C_API_H
#define NORDISKA_DELIVERY_C_API_PDF_GENERATOR_C_API_H

/*
 * =============================================================================
 * Nordiska Native PDF Generator — C ABI Public Interface
 * =============================================================================
 *
 * Overview:
 *   High-performance, unmanaged C-compatible library for generating customer-
 *   facing PDF documents (account statements, annual summaries, tax reports).
 *   Designed for direct FFI / P-Invoke integration from .NET and native callers.
 *
 * Architecture Boundary:
 *   - Presentation-only layer: consumes pre-calculated, host-formatted banking
 *     data. Native code does not compute taxes, interest, balances, or currency
 *     conversions.
 *   - C89 compatible ABI: exposes only primitive types, explicit structs, and
 *     function pointers. No C++ classes, STL types, or exceptions cross this
 *     boundary.
 *
 * Execution & Concurrency:
 *   - Synchronous on calling thread: each generation call executes entirely on
 *     the caller's thread without internal worker pools or thread hopping.
 *   - Independent call isolation: stateless across calls; concurrent threads
 *     can safely invoke generation for separate customer batches.
 *
 * Input Memory Contract (Caller Lifetime Guarantee):
 *   - The caller MUST guarantee that the memory buffer referenced by `json_utf8`
 *     remains allocated, valid, and unmodified for the entire duration of the
 *     synchronous `nordiska_pdf_v1_generate_customer_batch` call.
 *   - Native code inspects and borrows this memory during parsing and will not
 *     retain any pointer, reference, or dependency on `json_utf8` after the call
 *     returns.
 *
 * Schema & Document Versioning (Ground Truth):
 *   - The JSON payload envelope and document contents are strictly structured
 *     and versioned (using `$schema_version` at root and `version` per document).
 *   - For canonical structure and field definitions, see the ground-truth
 *     golden sample document: `golden_customer_batch_sample.json`
 *
 * Atomic Customer Batch Contract:
 *   - One invocation represents one customer and one or more documents.
 *   - All-or-nothing guarantee: the batch succeeds or fails as a unit. If any
 *     single document fails validation, layout, rendering, or limits, all
 *     in-progress output is discarded and zero callbacks are invoked.
 *   - Exactly-once callback: upon successful completion of all documents, the
 *     host callback is invoked exactly once with the complete in-memory batch.
 *
 * Output Memory Ownership & Buffer Lifetimes:
 *   - Native code retains and owns all batch buffers throughout generation and
 *     callback execution.
 *   - The host callback borrows the batch view and must copy or finish all use
 *     of PDF byte pointers before returning.
 *   - Native frees all memory immediately upon callback return. No pointers
 *     to batch structs or PDF bytes remain valid after the call returns.
 *
 * Error Reporting & Diagnostic Contract:
 *   - Diagnostic details are decoupled from the generation signature and stored
 *     in thread-local storage on the calling thread.
 *   - When `nordiska_pdf_v1_generate_customer_batch` returns a non-zero status code,
 *     the caller may invoke `nordiska_pdf_v1_get_last_error(void)` to retrieve a
 *     null-terminated UTF-8 diagnostic string.
 *   - Lifetime: The error string pointer is valid until the next generation call
 *     on the same calling thread. The caller must not free or mutate it.
 *   - If the previous generation call on the thread succeeded,
 *     `nordiska_pdf_v1_get_last_error(void)` returns an empty string ("").
 * =============================================================================
 */

#include <stddef.h>
#include <stdint.h>

#define NORDISKA_PDF_API __attribute__((visibility("default")))

#ifdef __cplusplus
extern "C" {
#endif

/* Status / error codes */
enum nordiska_pdf_status {
    NORDISKA_PDF_OK = 0,
    NORDISKA_PDF_INVALID_ARGUMENT = 1,
    NORDISKA_PDF_INVALID_INPUT = 2,
    NORDISKA_PDF_CALLBACK_FAILED = 3,
    NORDISKA_PDF_INTERNAL_ERROR = 4,
    NORDISKA_PDF_RESOURCE_LIMIT_EXCEEDED = 5,
    NORDISKA_PDF_OUT_OF_MEMORY = 6
};

/* View of an individual completed document within a customer batch */
struct nordiska_pdf_document_view {
    const char* document_id;
    const uint8_t* bytes;
    size_t length;
};

/* View of the complete customer batch delivered to the callback */
struct nordiska_pdf_batch_view {
    uint64_t customer_id;
    const struct nordiska_pdf_document_view* documents;
    size_t document_count;
};

/*
 * Delivery callback invoked synchronously on the calling thread EXACTLY ONCE
 * upon successful generation of every requested document in the customer batch.
 *
 * Parameters:
 *   batch     - Pointer to the completed customer batch view containing all
 *               rendered document byte views. Memory is owned by native code
 *               and guaranteed valid ONLY for the duration of this callback call.
 *               Native releases all batch memory immediately when this callback
 *               returns. The host must copy or transmit bytes before returning.
 *   user_data - Opaque caller-provided pointer forwarded verbatim from
 *               `nordiska_pdf_v1_generate_customer_batch`. Native code never
 *               dereferences, inspects, or modifies this pointer. Typically
 *               used to route output to a network socket, HTTP stream, or
 *               caller instance handle.
 *
 * Return value:
 *   Return 0 to indicate successful acceptance and delivery.
 *   Return non-zero to indicate callback handoff rejection (maps the API call
 *   return value to NORDISKA_PDF_CALLBACK_FAILED).
 */
typedef int (*nordiska_pdf_delivery_callback)(const struct nordiska_pdf_batch_view* batch, void* user_data);

/*
 * Generates an atomic customer PDF batch from a UTF-8 JSON payload.
 *
 * All-or-nothing guarantee:
 *   If any single document fails validation, layout, rendering, or signing,
 *   the entire batch is aborted, zero callbacks are invoked, and an error
 *   status code is returned.
 *
 * Concurrency:
 *   Executes entirely synchronously on the calling thread. No internal
 *   background threads or thread pools are spawned.
 *
 * Parameters:
 *   json_utf8   - Pointer to the UTF-8 encoded JSON payload defining the customer
 *                 batch. Must not be NULL. The caller guarantees this memory
 *                 remains valid, allocated, and unmodified for the duration
 *                 of this call.
 *   json_length - Byte length of `json_utf8`. Must be greater than 0 and less
 *                 than or equal to 32 MB (33,554,432 bytes).
 *   callback    - Delivery callback function to invoke upon batch completion.
 *                 Must not be NULL. Invoked exactly once on success; never
 *                 invoked on failure.
 *   user_data   - Opaque caller pointer passed through unchanged to `callback`.
 *                 May be NULL if the callback does not require caller state.
 *
 * Return value:
 *   Returns NORDISKA_PDF_OK (0) on success, or a non-zero nordiska_pdf_status
 *   code on failure.
 *
 * Error diagnostics:
 *   When returning non-zero, call `nordiska_pdf_v1_get_last_error(void)` on the
 *   same thread to retrieve a descriptive null-terminated error string.
 */
NORDISKA_PDF_API int nordiska_pdf_v1_generate_customer_batch(const uint8_t* json_utf8, size_t json_length,
                                                             nordiska_pdf_delivery_callback callback, void* user_data);

/* Resource limit queries for host admission control */
NORDISKA_PDF_API size_t nordiska_pdf_v1_max_json_bytes(void);

/* Returns thread-local diagnostic string for last error on calling thread; "" on success. Never NULL */
NORDISKA_PDF_API const char* nordiska_pdf_v1_get_last_error(void);

/* Returns human-readable status name (e.g. "NORDISKA_PDF_OK"), or "status code does not exist". Never NULL */
NORDISKA_PDF_API const char* nordiska_pdf_v1_status_name(int status);

#ifdef __cplusplus
}
#endif

#endif /* NORDISKA_DELIVERY_C_API_PDF_GENERATOR_C_API_H */
