#include "nordiska/c_api/pdf_generator_c_api.h"

#include "nordiska/application/pdf_generator.hpp"

#include <array>
#include <atomic>
#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <cstring>
#include <exception>
#include <expected>
#include <memory>
#include <new>
#include <span>
#include <string>
#include <string_view>
#include <unistd.h>
#include <utility>
#include <vector>

// Opaque caller-owned lifecycle state. Customer input and results remain local
// to each synchronous generation call; this object only retains components.
struct nordiska_pdf_generator {
    explicit nordiska_pdf_generator(nordiska::GeneratorConfig config) : generator(std::move(config)) {}
    nordiska::PdfGenerator generator;
    std::atomic_flag in_use{};
};

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
                                                     bool has_callback) noexcept {
    if (json_utf8 == nullptr) {
        set_last_error("json_utf8 must not be null");
        return std::unexpected(NORDISKA_PDF_INVALID_ARGUMENT);
    }
    if (json_length == 0) {
        set_last_error("json_length must be greater than zero");
        return std::unexpected(NORDISKA_PDF_INVALID_ARGUMENT);
    }
    if (!has_callback) {
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
    try {
        set_last_error(err.message());
    } catch (...) {
        set_last_error_static("Unknown error occurred");
    }
    return std::visit(
        [](const auto& payload) noexcept -> int {
            using T = std::decay_t<decltype(payload)>;
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
                return NORDISKA_PDF_INTERNAL_ERROR;
            } else if constexpr (std::is_same_v<T, nordiska::SigningError>) {
                return NORDISKA_PDF_SIGNING_FAILED;
            }
        },
        err.details);
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

int deliver_batch(const nordiska::GeneratedPdfs& completed_batch, nordiska_pdf_delivery_callback_v2 callback,
                  void* user_data) {
    std::vector<std::array<char, 65>> hashes(completed_batch.documents.size());
    std::vector<nordiska_pdf_document_view_v2> document_views;
    document_views.reserve(completed_batch.documents.size());
    constexpr char hex[] = "0123456789abcdef";
    for (size_t i = 0; i < completed_batch.documents.size(); ++i) {
        const auto& doc = completed_batch.documents[i];
        auto& hash = hashes[i];
        for (size_t j = 0; j < doc.sha256_hash.size(); ++j) {
            hash[j * 2] = hex[doc.sha256_hash[j] >> 4];
            hash[j * 2 + 1] = hex[doc.sha256_hash[j] & 0x0f];
        }
        hash[64] = '\0';
        document_views.push_back(nordiska_pdf_document_view_v2{
            .document_id = doc.document_id.c_str(),
            .bytes = doc.pdf_bytes.data(),
            .length = doc.pdf_bytes.size(),
            .sha256_hex = hash.data(),
        });
    }
    const nordiska_pdf_batch_view_v2 batch_view{
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

std::expected<nordiska::GeneratorConfig, int> resolve_config() {
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
            set_last_error("NORDISKA_PDF_ENGINE must be native, libharu, haru, or cairo");
            return std::unexpected(NORDISKA_PDF_INVALID_ARGUMENT);
        }
    }

    return nordiska::GeneratorConfig{
        .ingestor = nordiska::JsonIngestorKind::Simdjson,
        .engine = engine,
        .enable_signing = enable_signing,
        .compression = true, // Cut output size by 60% for a 25% performance penalty, huge win
    };
}

// All exported status-returning functions contain exceptions at the C boundary.
template <typename Operation> int invoke_boundary(Operation&& operation) noexcept {
    clear_last_error();
    try {
        const int status = operation();
        if (status == NORDISKA_PDF_OK) {
            clear_last_error();
        }
        return status;
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

template <typename Callback>
int generate_and_deliver(nordiska::PdfGenerator& generator, std::span<const uint8_t> payload, Callback callback,
                         void* user_data) {
    auto batch = generator.generate(payload);
    if (!batch) {
        return map_generator_error(batch.error());
    }
    return deliver_batch(*batch, callback, user_data);
}

template <typename Callback>
int generate_customer_batch(const uint8_t* json_utf8, size_t json_length, Callback callback, void* user_data) {
    return invoke_boundary([&]() -> int {
        auto validation = validate_boundary_arguments(json_utf8, json_length, callback != nullptr);
        if (!validation) {
            return validation.error();
        }
        auto config = resolve_config();
        if (!config) {
            return config.error();
        }
        nordiska::PdfGenerator generator(std::move(*config));
        return generate_and_deliver(generator, {json_utf8, json_length}, callback, user_data);
    });
}

class GeneratorUseGuard {
  public:
    explicit GeneratorUseGuard(std::atomic_flag& flag) : flag_(flag) {}
    ~GeneratorUseGuard() {
        flag_.clear(std::memory_order_release);
    }
    GeneratorUseGuard(const GeneratorUseGuard&) = delete;
    GeneratorUseGuard& operator=(const GeneratorUseGuard&) = delete;

  private:
    std::atomic_flag& flag_;
};

template <typename Callback>
int generate_with_handle(nordiska_pdf_generator* handle, const uint8_t* json_utf8, size_t json_length,
                         Callback callback, void* user_data) {
    return invoke_boundary([&]() -> int {
        if (!handle) {
            set_last_error("generator handle must not be null");
            return NORDISKA_PDF_INVALID_ARGUMENT;
        }
        auto validation = validate_boundary_arguments(json_utf8, json_length, callback != nullptr);
        if (!validation) {
            return validation.error();
        }
        if (handle->in_use.test_and_set(std::memory_order_acquire)) {
            set_last_error("generator handle is already in use");
            return NORDISKA_PDF_INVALID_ARGUMENT;
        }
        GeneratorUseGuard guard(handle->in_use);
        return generate_and_deliver(handle->generator, {json_utf8, json_length}, callback, user_data);
    });
}

} // namespace

extern "C" int nordiska_pdf_v1_generate_customer_batch(const uint8_t* json_utf8, size_t json_length,
                                                       nordiska_pdf_delivery_callback callback, void* user_data) {
    return generate_customer_batch(json_utf8, json_length, callback, user_data);
}

extern "C" int nordiska_pdf_v2_generate_customer_batch(const uint8_t* json_utf8, size_t json_length,
                                                       nordiska_pdf_delivery_callback_v2 callback, void* user_data) {
    return generate_customer_batch(json_utf8, json_length, callback, user_data);
}

extern "C" int nordiska_pdf_v2_create_generator(nordiska_pdf_generator** out_generator) {
    return invoke_boundary([&]() -> int {
        if (!out_generator) {
            set_last_error("out_generator must not be null");
            return NORDISKA_PDF_INVALID_ARGUMENT;
        }
        *out_generator = nullptr;
        auto config = resolve_config();
        if (!config) {
            return config.error();
        }
        *out_generator = std::make_unique<nordiska_pdf_generator>(std::move(*config)).release();
        return NORDISKA_PDF_OK;
    });
}

extern "C" void nordiska_pdf_v2_destroy_generator(nordiska_pdf_generator* generator) {
    delete generator;
}

extern "C" int nordiska_pdf_v1_generate_customer_batch_with_generator(nordiska_pdf_generator* generator,
                                                                      const uint8_t* json_utf8, size_t json_length,
                                                                      nordiska_pdf_delivery_callback callback,
                                                                      void* user_data) {
    return generate_with_handle(generator, json_utf8, json_length, callback, user_data);
}

extern "C" int nordiska_pdf_v2_generate_customer_batch_with_generator(nordiska_pdf_generator* generator,
                                                                      const uint8_t* json_utf8, size_t json_length,
                                                                      nordiska_pdf_delivery_callback_v2 callback,
                                                                      void* user_data) {
    return generate_with_handle(generator, json_utf8, json_length, callback, user_data);
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
