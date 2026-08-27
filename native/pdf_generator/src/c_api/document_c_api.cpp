#include "nordiska/delivery/c_api/document_c_api.h"

#include "nordiska/adapters/input/json_input_adapter.hpp"
#include "nordiska/adapters/output/byte_sinks.hpp"
#include "nordiska/application/generate_documents.hpp"
#include "nordiska/composition/default_composition.hpp"

#include <algorithm>
#include <cstring>
#include <exception>
#include <span>
#include <stdexcept>
#include <string>
#include <string_view>
#include <utility>
#include <vector>

namespace {

void write_error(char* buffer, size_t length, const std::string& message) {
    if (buffer == nullptr || length == 0) {
        return;
    }
    const size_t copy_length = std::min(length - 1, message.size());
    std::memcpy(buffer, message.data(), copy_length);
    buffer[copy_length] = '\0';
}

class CallbackRejected final : public std::runtime_error {
  public:
    CallbackRejected() : std::runtime_error("document callback rejected PDF bytes") {}
};

} // namespace

extern "C" int nordiska_document_generate_json(const uint8_t* json_utf8, size_t json_length,
                                               nordiska_document_callback callback,
                                               void* callback_context, char* error_buffer,
                                               size_t error_buffer_length) {
    if (error_buffer != nullptr && error_buffer_length > 0) {
        error_buffer[0] = '\0';
    }
    if (json_utf8 == nullptr || json_length == 0 || callback == nullptr) {
        write_error(error_buffer, error_buffer_length,
                    "json_utf8, json_length, and callback are required");
        return NORDISKA_DOCUMENT_INVALID_ARGUMENT;
    }

    try {
        const std::string_view json(reinterpret_cast<const char*>(json_utf8), json_length);
        nordiska::JsonInputAdapter input;
        const nordiska::Report report = input.import_text(json);
        const std::vector<nordiska::DocumentRequest> requests{{std::move(report)}};
        bool callback_failed = false;
        nordiska::CallbackOutputDestination destination(
            [callback, callback_context, &callback_failed](std::span<const std::byte> bytes,
                                                           std::size_t index) {
                const int callback_status = callback(reinterpret_cast<const uint8_t*>(bytes.data()),
                                                     bytes.size(), index, callback_context);
                if (callback_status != 0) {
                    callback_failed = true;
                    throw CallbackRejected();
                }
            });
        nordiska::GenerateDocuments generate(
            [] { return nordiska::make_default_document_renderer(); }, 1);
        const auto results = generate.execute(requests, destination);
        if (!results.front().succeeded) {
            if (callback_failed) {
                throw CallbackRejected();
            }
            if (results.front().failure == nordiska::DocumentFailure::invalid_input) {
                throw std::invalid_argument(results.front().error);
            }
            throw std::runtime_error(results.front().error);
        }
        return NORDISKA_DOCUMENT_OK;
    } catch (const CallbackRejected& error) {
        write_error(error_buffer, error_buffer_length, error.what());
        return NORDISKA_DOCUMENT_CALLBACK_FAILED;
    } catch (const nordiska::JsonInputError& error) {
        write_error(error_buffer, error_buffer_length, error.what());
        return NORDISKA_DOCUMENT_INVALID_INPUT;
    } catch (const std::invalid_argument& error) {
        write_error(error_buffer, error_buffer_length, error.what());
        return NORDISKA_DOCUMENT_INVALID_INPUT;
    } catch (const std::exception& error) {
        write_error(error_buffer, error_buffer_length, error.what());
        return NORDISKA_DOCUMENT_INTERNAL_ERROR;
    } catch (...) {
        write_error(error_buffer, error_buffer_length, "unknown native document generation error");
        return NORDISKA_DOCUMENT_INTERNAL_ERROR;
    }
}

extern "C" const char* nordiska_document_version(void) {
    return "1.0.0";
}
