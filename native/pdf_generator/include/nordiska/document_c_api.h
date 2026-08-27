#pragma once

#include <stddef.h>
#include <stdint.h>

#if defined(_WIN32)
#define NORDISKA_DOCUMENT_API __declspec(dllexport)
#else
#define NORDISKA_DOCUMENT_API __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

enum nordiska_document_status {
    NORDISKA_DOCUMENT_OK = 0,
    NORDISKA_DOCUMENT_INVALID_ARGUMENT = 1,
    NORDISKA_DOCUMENT_INVALID_INPUT = 2,
    NORDISKA_DOCUMENT_CALLBACK_FAILED = 3,
    NORDISKA_DOCUMENT_INTERNAL_ERROR = 4,
};

/* Invoked synchronously once for each completed PDF. Return zero to accept it. */
typedef int (*nordiska_document_callback)(const uint8_t* bytes, size_t length,
                                          size_t document_index, void* context);

/*
 * Generates PDFs from one JSON report object. All arguments are borrowed for
 * this call only. The callback and its bytes are valid only during the call.
 * Errors are UTF-8 and truncated to fit error_buffer (including its NUL).
 */
NORDISKA_DOCUMENT_API int
nordiska_document_generate_json(const uint8_t* json_utf8, size_t json_length,
                                nordiska_document_callback callback, void* callback_context,
                                char* error_buffer, size_t error_buffer_length);

NORDISKA_DOCUMENT_API const char* nordiska_document_version(void);

#ifdef __cplusplus
}
#endif
