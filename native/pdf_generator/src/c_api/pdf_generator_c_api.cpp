#include "nordiska/c_api/pdf_generator_c_api.h"

#include "nordiska/application/pdf_generator.hpp"

#include <array>
#include <cctype>
#include <cerrno>
#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <cstring>
#include <exception>
#include <expected>
#include <new>
#include <span>
#include <string>
#include <string_view>
#include <type_traits>
#include <unistd.h>
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

constexpr size_t kDefaultMaxJsonPayloadBytes = 32 * 1024 * 1024; // 32 MB (NOR-157)

inline size_t parse_env_size(const char* env_var_name, size_t default_value) noexcept {
    if (const char* val = std::getenv(env_var_name); val != nullptr && *val != '\0') {
        const char* p = val;
        while (*p != '\0' && std::isspace(static_cast<unsigned char>(*p))) {
            ++p;
        }
        if (*p == '-' || *p == '\0') {
            return default_value;
        }
        char* end = nullptr;
        errno = 0;
        const unsigned long long parsed = std::strtoull(p, &end, 10);
        if (errno == 0 && end != p && *end == '\0' && parsed > 0) {
            return static_cast<size_t>(parsed);
        }
    }
    return default_value;
}

size_t get_max_json_payload_bytes() noexcept {
    return parse_env_size("NORDISKA_PDF_MAX_PAYLOAD_BYTES", kDefaultMaxJsonPayloadBytes);
}

// Only structured diagnostics can enter thread-local error storage.
thread_local nordiska::DiagnosticText g_last_error;
thread_local nordiska::PipelineTiming g_last_timing{};

void clear_last_error() noexcept {
    g_last_error = {};
}
void set_last_error(nordiska::Diagnostic diagnostic) noexcept {
    g_last_error = nordiska::format_diagnostic(diagnostic);
}

std::expected<void, int> validate_boundary_arguments(const uint8_t* json_utf8, size_t json_length,
                                                     bool has_callback) noexcept {
    if (json_utf8 == nullptr) {
        set_last_error({nordiska::DiagnosticCode::Required,
                        nordiska::DiagnosticStage::Boundary,
                        {.field = nordiska::DiagnosticField::JsonUtf8}});
        return std::unexpected(NORDISKA_PDF_INVALID_ARGUMENT);
    }
    if (json_length == 0) {
        set_last_error({nordiska::DiagnosticCode::MustBePositive,
                        nordiska::DiagnosticStage::Boundary,
                        {.field = nordiska::DiagnosticField::JsonLength}});
        return std::unexpected(NORDISKA_PDF_INVALID_ARGUMENT);
    }
    if (!has_callback) {
        set_last_error({nordiska::DiagnosticCode::Required,
                        nordiska::DiagnosticStage::Boundary,
                        {.field = nordiska::DiagnosticField::Callback}});
        return std::unexpected(NORDISKA_PDF_INVALID_ARGUMENT);
    }
    if (json_length > get_max_json_payload_bytes()) {
        set_last_error({nordiska::DiagnosticCode::PayloadTooLarge});
        return std::unexpected(NORDISKA_PDF_RESOURCE_LIMIT_EXCEEDED);
    }
    return {};
}

int map_generator_error(const nordiska::GeneratorError& err) noexcept {
    set_last_error(err.diagnostic());
    return std::visit(
        [](const auto& payload) noexcept -> int {
            using T = std::decay_t<decltype(payload)>;
            if (payload.code == nordiska::DiagnosticCode::OutOfMemory) {
                return NORDISKA_PDF_OUT_OF_MEMORY;
            }
            if constexpr (std::is_same_v<T, nordiska::InvalidArgumentError>) {
                return NORDISKA_PDF_INVALID_ARGUMENT;
            } else if constexpr (std::is_same_v<T, nordiska::ResourceLimitError>) {
                return NORDISKA_PDF_RESOURCE_LIMIT_EXCEEDED;
            } else if constexpr (std::is_same_v<T, nordiska::IngestError>) {
                return payload.kind == nordiska::IngestErrorKind::InternalError ? NORDISKA_PDF_INTERNAL_ERROR
                                                                                : NORDISKA_PDF_INVALID_INPUT;
            } else if constexpr (std::is_same_v<T, nordiska::LayoutError>) {
                return payload.kind == nordiska::LayoutErrorKind::InternalError ? NORDISKA_PDF_INTERNAL_ERROR
                                                                                : NORDISKA_PDF_INVALID_INPUT;
            } else if constexpr (std::is_same_v<T, nordiska::RenderError>) {
                if (payload.kind == nordiska::RenderErrorKind::OutOfMemory) {
                    return NORDISKA_PDF_RESOURCE_LIMIT_EXCEEDED;
                }
                return NORDISKA_PDF_INTERNAL_ERROR;
            } else if constexpr (std::is_same_v<T, nordiska::SigningError>) {
                return NORDISKA_PDF_SIGNING_FAILED;
            } else {
                return NORDISKA_PDF_INTERNAL_ERROR;
            }
        },
        err.details);
}

// V1 and V2 share delivery bookkeeping; only V2 exposes the artifact hash.
template <typename Callback>
int deliver_batch(const nordiska::GeneratedPdfs& completed_batch, Callback callback, void* user_data) {
    constexpr bool with_hash = std::is_same_v<Callback, nordiska_pdf_delivery_callback_v2>;
    using DocumentView = std::conditional_t<with_hash, nordiska_pdf_document_view_v2, nordiska_pdf_document_view>;
    using BatchView = std::conditional_t<with_hash, nordiska_pdf_batch_view_v2, nordiska_pdf_batch_view>;
    std::vector<DocumentView> document_views;
    document_views.reserve(completed_batch.documents.size());
    std::vector<std::array<char, 65>> hashes;
    if constexpr (with_hash) {
        hashes.resize(completed_batch.documents.size());
    }

    for (size_t i = 0; i < completed_batch.documents.size(); ++i) {
        const auto& doc = completed_batch.documents[i];
        DocumentView view{
            .document_id = doc.document_id.c_str(),
            .bytes = doc.pdf_bytes.data(),
            .length = doc.pdf_bytes.size(),
        };
        if constexpr (with_hash) {
            constexpr char hex[] = "0123456789abcdef";
            auto& hash = hashes[i];
            for (size_t j = 0; j < doc.sha256_hash.size(); ++j) {
                hash[j * 2] = hex[doc.sha256_hash[j] >> 4];
                hash[j * 2 + 1] = hex[doc.sha256_hash[j] & 0x0f];
            }
            hash[64] = '\0';
            view.sha256_hex = hash.data();
        }
        document_views.push_back(view);
    }
    const BatchView batch_view{
        .customer_id = completed_batch.customer_id,
        .documents = document_views.data(),
        .document_count = document_views.size(),
    };
    const int cb_status = callback(&batch_view, user_data);
    if (cb_status != 0) {
        set_last_error({nordiska::DiagnosticCode::CallbackRejected, nordiska::DiagnosticStage::Delivery});
        return NORDISKA_PDF_CALLBACK_FAILED;
    }
    // A reentrant callback may have completed a failing native call on this
    // thread. The successfully completed outer call owns the final snapshot.
    clear_last_error();
    return NORDISKA_PDF_OK;
}

template <typename Callback>
int generate_customer_batch(const uint8_t* json_utf8, size_t json_length, Callback callback, void* user_data,
                            nordiska::PipelineTiming* timing) {
    clear_last_error(); // clear thread local error before we start

    try {
        auto val_res = validate_boundary_arguments(json_utf8, json_length, callback != nullptr);
        if (!val_res) {
            return val_res.error();
        }

        const std::span<const uint8_t> payload_span{json_utf8, json_length};

        const char* sign_env = std::getenv("NORDISKA_PDF_ENABLE_SIGNING");
        bool enable_signing = false;
        if (sign_env != nullptr) {
            enable_signing = (std::strcmp(sign_env, "1") == 0 || strcasecmp(sign_env, "true") == 0);
        } else {
            const char* socket_path = std::getenv("PDF_SIGNER_SOCKET_PATH");
            if (socket_path == nullptr || socket_path[0] == '\0') {
                socket_path = "/run/pdf-signer/pdf-signer.sock";
            }
            if (::access(socket_path, R_OK | W_OK) == 0) {
                enable_signing = true;
            }
        }

        auto engine = nordiska::PdfEngineKind::Libharu;
        if (const char* engine_env = std::getenv("NORDISKA_PDF_ENGINE"); engine_env && engine_env[0] != '\0') {
            if (strcasecmp(engine_env, "native") == 0) {
                engine = nordiska::PdfEngineKind::Native;
            } else if (strcasecmp(engine_env, "cairo") == 0) {
                engine = nordiska::PdfEngineKind::Cairo;
            } else if (strcasecmp(engine_env, "libharu") != 0 && strcasecmp(engine_env, "haru") != 0) {
                set_last_error({nordiska::DiagnosticCode::InvalidEngine,
                                nordiska::DiagnosticStage::Boundary,
                                {.field = nordiska::DiagnosticField::Engine}});
                return NORDISKA_PDF_INVALID_ARGUMENT;
            }
        }

        const nordiska::GeneratorConfig config{
            .ingestor = nordiska::JsonIngestorKind::Simdjson,
            .engine = engine,
            .enable_signing = enable_signing,
            .compression = true, // Cut output size by 60% for a 25% performance penalty, huge win
        };

        // Generate PDFs
        nordiska::PdfGenerator generator(config);
        auto batch_result = generator.generate(payload_span, timing);
        if (!batch_result) {
            return map_generator_error(batch_result.error());
        }

        return deliver_batch(*batch_result, callback, user_data);

    } catch (const std::bad_alloc&) {
        set_last_error({nordiska::DiagnosticCode::OutOfMemory});
        return NORDISKA_PDF_OUT_OF_MEMORY;
    } catch (const std::exception&) {
        set_last_error({nordiska::DiagnosticCode::UnexpectedFailure});
        return NORDISKA_PDF_INTERNAL_ERROR;
    } catch (...) {
        set_last_error({nordiska::DiagnosticCode::UnexpectedFailure});
        return NORDISKA_PDF_INTERNAL_ERROR;
    }
}

template <typename Callback>
int generate_instrumented_customer_batch(const uint8_t* json_utf8, size_t json_length, Callback callback,
                                         void* user_data) {
    // Per-call collection avoids mixing nested instrumented calls. Publish the
    // snapshot on return so the getter always describes the last completed call.
    nordiska::PipelineTiming timing{};
    const int status = generate_customer_batch(json_utf8, json_length, callback, user_data, &timing);
    g_last_timing = timing;
    return status;
}

} // namespace

extern "C" int nordiska_pdf_v1_generate_customer_batch(const uint8_t* json_utf8, size_t json_length,
                                                       nordiska_pdf_delivery_callback callback, void* user_data) {
    return generate_customer_batch(json_utf8, json_length, callback, user_data, nullptr);
}

extern "C" int nordiska_pdf_v2_generate_customer_batch(const uint8_t* json_utf8, size_t json_length,
                                                       nordiska_pdf_delivery_callback_v2 callback, void* user_data) {
    return generate_customer_batch(json_utf8, json_length, callback, user_data, nullptr);
}

extern "C" int nordiska_pdf_v1_generate_customer_batch_instrumented(const uint8_t* json_utf8, size_t json_length,
                                                                    nordiska_pdf_delivery_callback callback,
                                                                    void* user_data) {
    return generate_instrumented_customer_batch(json_utf8, json_length, callback, user_data);
}

extern "C" int nordiska_pdf_v2_generate_customer_batch_instrumented(const uint8_t* json_utf8, size_t json_length,
                                                                    nordiska_pdf_delivery_callback_v2 callback,
                                                                    void* user_data) {
    return generate_instrumented_customer_batch(json_utf8, json_length, callback, user_data);
}

extern "C" size_t nordiska_pdf_v1_max_json_bytes(void) {
    return get_max_json_payload_bytes();
}

extern "C" const char* nordiska_pdf_v1_get_last_error(void) {
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

extern "C" void nordiska_pdf_v1_get_last_timing(nordiska_pdf_pipeline_timing* out_timing) {
    if (out_timing == nullptr) {
        return;
    }
    out_timing->ingest_seconds = g_last_timing.ingest_seconds;
    out_timing->layout_seconds = g_last_timing.layout_seconds;
    out_timing->render_seconds = g_last_timing.render_seconds;
    out_timing->sign_seconds = g_last_timing.sign_seconds;
    out_timing->signer_call_seconds = g_last_timing.signer_call_seconds;
    out_timing->prepare_seconds = g_last_timing.prepare_seconds;
    out_timing->digest_seconds = g_last_timing.digest_seconds;
    out_timing->checksum_seconds = g_last_timing.checksum_seconds;
    out_timing->total_seconds = g_last_timing.total_seconds();
}
