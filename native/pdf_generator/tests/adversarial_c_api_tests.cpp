#include "nordiska/c_api/pdf_generator_c_api.h"
#include "pdf_test_helpers.hpp"

#include <array>
#include <atomic>
#include <cstdint>
#include <cstdlib>
#include <cstring>
#include <iostream>
#include <openssl/sha.h>
#include <optional>
#include <stdexcept>
#include <string>
#include <thread>
#include <vector>

namespace {

constexpr int NORDISKA_PDF_OK = 0;
constexpr int NORDISKA_PDF_INVALID_ARGUMENT = 1;
constexpr int NORDISKA_PDF_INVALID_INPUT = 2;
constexpr int NORDISKA_PDF_CALLBACK_FAILED = 3;
constexpr int NORDISKA_PDF_INTERNAL_ERROR = 4;
constexpr int NORDISKA_PDF_RESOURCE_LIMIT_EXCEEDED = 5;
constexpr int NORDISKA_PDF_OUT_OF_MEMORY = 6;
constexpr int NORDISKA_PDF_SIGNING_FAILED = 7;

void require(bool condition, const char* message) {
    if (!condition) {
        std::cerr << "Assertion failed: " << message << std::endl;
        throw std::runtime_error(message);
    }
}

class ScopedEnv {
  public:
    ScopedEnv(const char* name, const char* value) : name_(name) {
        if (const char* old = std::getenv(name)) {
            old_ = old;
        }
        set(value);
    }
    ~ScopedEnv() {
        set(old_ ? old_->c_str() : nullptr);
    }
    void set(const char* val) const {
        if (val) {
            ::setenv(name_, val, 1);
        } else {
            ::unsetenv(name_);
        }
    }

  private:
    const char* name_;
    std::optional<std::string> old_;
};

struct BatchCaptureV1 {
    uint64_t customer_id{0};
    size_t call_count{0};
    struct Doc {
        std::string id;
        std::vector<uint8_t> bytes;
    };
    std::vector<Doc> docs;
};

int v1_success_cb(const struct nordiska_pdf_batch_view* batch, void* user_data) {
    auto* cap = static_cast<BatchCaptureV1*>(user_data);
    cap->call_count++;
    cap->customer_id = batch->customer_id;
    for (size_t i = 0; i < batch->document_count; ++i) {
        const auto& d = batch->documents[i];
        cap->docs.push_back({
            .id = d.document_id ? d.document_id : "",
            .bytes = std::vector<uint8_t>(d.bytes, d.bytes + d.length),
        });
    }
    return 0;
}

int v1_reject_cb(const struct nordiska_pdf_batch_view*, void*) {
    return -99;
}

struct BatchCaptureV2 {
    uint64_t customer_id{0};
    size_t call_count{0};
    struct Doc {
        std::string id;
        std::vector<uint8_t> bytes;
        std::string sha256_hex;
    };
    std::vector<Doc> docs;
};

int v2_success_cb(const struct nordiska_pdf_batch_view_v2* batch, void* user_data) {
    auto* cap = static_cast<BatchCaptureV2*>(user_data);
    cap->call_count++;
    cap->customer_id = batch->customer_id;
    for (size_t i = 0; i < batch->document_count; ++i) {
        const auto& d = batch->documents[i];
        cap->docs.push_back({
            .id = d.document_id ? d.document_id : "",
            .bytes = std::vector<uint8_t>(d.bytes, d.bytes + d.length),
            .sha256_hex = d.sha256_hex ? d.sha256_hex : "",
        });
    }
    return 0;
}

const std::string kValidBatch = R"({
    "customer_id": 888,
    "documents": [
        {
            "document_id": "doc_statement",
            "kind": "account_statement",
            "document": {
                "account_number": "SE123456",
                "title": "Kontoutdrag",
                "period": "2026-01-01 - 2026-01-31",
                "closing_balance": "50 000,00 SEK",
                "transactions": [
                    { "date": "2026-01-10", "description": "Matvaror", "amount_minor": -25000, "amount_display": "−250,00 SEK" }
                ]
            }
        },
        {
            "document_id": "doc_tax",
            "kind": "annual_tax_report",
            "document": {
                "account_number": "SE123456",
                "tax_year": "2025",
                "total_interest_earned": "1 200,00 SEK",
                "preliminary_tax_deducted": "360,00 SEK"
            }
        }
    ]
})";

void test_boundary_arguments() {
    std::cout << "Testing C ABI boundary argument validation..." << std::endl;
    BatchCaptureV1 cap1;
    BatchCaptureV2 cap2;

    const auto* valid_bytes = reinterpret_cast<const uint8_t*>(kValidBatch.data());
    const size_t valid_len = kValidBatch.size();

    // 1. Null pointers
    require(nordiska_pdf_v1_generate_customer_batch(nullptr, valid_len, v1_success_cb, &cap1) ==
                NORDISKA_PDF_INVALID_ARGUMENT,
            "V1 must reject null json_utf8");
    require(nordiska_pdf_v1_generate_customer_batch(valid_bytes, valid_len, nullptr, nullptr) ==
                NORDISKA_PDF_INVALID_ARGUMENT,
            "V1 must reject null callback");
    require(nordiska_pdf_v2_generate_customer_batch(nullptr, valid_len, v2_success_cb, &cap2) ==
                NORDISKA_PDF_INVALID_ARGUMENT,
            "V2 must reject null json_utf8");
    require(nordiska_pdf_v2_generate_customer_batch(valid_bytes, valid_len, nullptr, nullptr) ==
                NORDISKA_PDF_INVALID_ARGUMENT,
            "V2 must reject null callback");

    // 2. Zero length
    require(nordiska_pdf_v1_generate_customer_batch(valid_bytes, 0, v1_success_cb, &cap1) ==
                NORDISKA_PDF_INVALID_ARGUMENT,
            "V1 must reject 0 json_length");
    require(nordiska_pdf_v2_generate_customer_batch(valid_bytes, 0, v2_success_cb, &cap2) ==
                NORDISKA_PDF_INVALID_ARGUMENT,
            "V2 must reject 0 json_length");

    // 3. Length exceeds 32 MB limit
    const size_t max_bytes = nordiska_pdf_v1_max_json_bytes();
    require(max_bytes == 32 * 1024 * 1024, "Max bytes must be 32 MB");
    require(nordiska_pdf_v1_generate_customer_batch(valid_bytes, max_bytes + 1, v1_success_cb, &cap1) ==
                NORDISKA_PDF_RESOURCE_LIMIT_EXCEEDED,
            "V1 must reject payload exceeding 32 MB");
    require(nordiska_pdf_v2_generate_customer_batch(valid_bytes, max_bytes + 1, v2_success_cb, &cap2) ==
                NORDISKA_PDF_RESOURCE_LIMIT_EXCEEDED,
            "V2 must reject payload exceeding 32 MB");

    // Extreme SIZE_MAX
    require(nordiska_pdf_v1_generate_customer_batch(valid_bytes, SIZE_MAX, v1_success_cb, &cap1) ==
                NORDISKA_PDF_RESOURCE_LIMIT_EXCEEDED,
            "V1 must reject SIZE_MAX without overflowing");
    require(nordiska_pdf_v2_generate_customer_batch(valid_bytes, SIZE_MAX, v2_success_cb, &cap2) ==
                NORDISKA_PDF_RESOURCE_LIMIT_EXCEEDED,
            "V2 must reject SIZE_MAX without overflowing");

    // Ensure no callbacks were ever called during failed validations
    require(cap1.call_count == 0, "No V1 callback should have been invoked on boundary error");
    require(cap2.call_count == 0, "No V2 callback should have been invoked on boundary error");
}

void test_callback_rejections_and_hashes() {
    std::cout << "Testing callback rejection and V2 artifact hashes..." << std::endl;
    ScopedEnv signing("NORDISKA_PDF_ENABLE_SIGNING", "0");
    const auto* valid_bytes = reinterpret_cast<const uint8_t*>(kValidBatch.data());

    // 1. Callback rejects batch
    require(nordiska_pdf_v1_generate_customer_batch(valid_bytes, kValidBatch.size(), v1_reject_cb, nullptr) ==
                NORDISKA_PDF_CALLBACK_FAILED,
            "V1 must return CALLBACK_FAILED when callback rejects");
    require(std::string(nordiska_pdf_v1_get_last_error()).find("-99") != std::string::npos,
            "V1 last error must report callback status code");

    // 2. V2 valid execution and hash verification
    BatchCaptureV2 cap2;
    require(nordiska_pdf_v2_generate_customer_batch(valid_bytes, kValidBatch.size(), v2_success_cb, &cap2) ==
                NORDISKA_PDF_OK,
            "V2 must succeed for valid batch");
    require(cap2.call_count == 1, "V2 callback must be called exactly once");
    require(cap2.customer_id == 888, "Customer ID must match");
    require(cap2.docs.size() == 2, "Must deliver 2 documents");

    for (const auto& doc : cap2.docs) {
        require(nordiska::test::is_valid_pdf_structure(doc.bytes), "Delivered PDF must be valid");
        require(nordiska::test::validate_pdf_with_ghostscript(doc.bytes), "Delivered PDF must pass Ghostscript");

        // Verify SHA-256 hash
        require(doc.sha256_hex.size() == 64, "SHA-256 hex must be 64 characters");
        std::array<unsigned char, SHA256_DIGEST_LENGTH> digest;
        SHA256(doc.bytes.data(), doc.bytes.size(), digest.data());
        constexpr char hex[] = "0123456789abcdef";
        std::string expected_hash;
        for (auto b : digest) {
            expected_hash += hex[b >> 4];
            expected_hash += hex[b & 0x0f];
        }
        require(doc.sha256_hex == expected_hash, "Reported sha256_hex must match actual bytes");
    }
}

void test_environment_variable_tampering() {
    std::cout << "Testing environment variable tampering..." << std::endl;
    ScopedEnv signing("NORDISKA_PDF_ENABLE_SIGNING", "0");
    ScopedEnv engine("NORDISKA_PDF_ENGINE", nullptr);

    const auto* valid_bytes = reinterpret_cast<const uint8_t*>(kValidBatch.data());
    BatchCaptureV1 cap;

    // 1. Valid case variations
    for (const char* valid_engine : {"native", "NATIVE", "Native", "libharu", "LIBHARU", "haru", "cairo", "CAIRO"}) {
        engine.set(valid_engine);
        cap = {};
        require(nordiska_pdf_v1_generate_customer_batch(valid_bytes, kValidBatch.size(), v1_success_cb, &cap) ==
                    NORDISKA_PDF_OK,
                ("Engine " + std::string(valid_engine) + " must succeed").c_str());
        require(cap.call_count == 1, "Must deliver batch");
    }

    // 2. Invalid engine names
    for (const char* invalid_engine : {"unknown", "v8", "chromium", "  native  ", "native_engine"}) {
        engine.set(invalid_engine);
        cap = {};
        require(nordiska_pdf_v1_generate_customer_batch(valid_bytes, kValidBatch.size(), v1_success_cb, &cap) ==
                    NORDISKA_PDF_INVALID_ARGUMENT,
                ("Invalid engine " + std::string(invalid_engine) + " must be rejected").c_str());
        require(cap.call_count == 0, "No callback must be called on invalid engine");
    }

    // 3. Massive 50,000-character engine name string
    std::string massive_engine(50000, 'X');
    engine.set(massive_engine.c_str());
    cap = {};
    require(nordiska_pdf_v1_generate_customer_batch(valid_bytes, kValidBatch.size(), v1_success_cb, &cap) ==
                NORDISKA_PDF_INVALID_ARGUMENT,
            "Massive engine name must be rejected without crash");
    require(cap.call_count == 0, "No callback must be called");

    // 4. Payload size limit override via NORDISKA_PDF_MAX_PAYLOAD_BYTES
    {
        require(nordiska_pdf_v1_max_json_bytes() == 32 * 1024 * 1024, "Default max payload must be 32 MB");

        ScopedEnv custom_max("NORDISKA_PDF_MAX_PAYLOAD_BYTES", "512");
        require(nordiska_pdf_v1_max_json_bytes() == 512, "max_json_bytes must reflect NORDISKA_PDF_MAX_PAYLOAD_BYTES");
        cap = {};
        require(nordiska_pdf_v1_generate_customer_batch(valid_bytes, kValidBatch.size(), v1_success_cb, &cap) ==
                    NORDISKA_PDF_RESOURCE_LIMIT_EXCEEDED,
                "Batch exceeding custom NORDISKA_PDF_MAX_PAYLOAD_BYTES must return RESOURCE_LIMIT_EXCEEDED");
        require(cap.call_count == 0, "No callback must be invoked when payload exceeds limit");

        // Invalid or zero values safely fall back to 32 MB default
        custom_max.set("not_a_number");
        require(nordiska_pdf_v1_max_json_bytes() == 32 * 1024 * 1024, "Invalid env value must fallback to default");

        custom_max.set("0");
        require(nordiska_pdf_v1_max_json_bytes() == 32 * 1024 * 1024, "Zero env value must fallback to default");

        custom_max.set("-100");
        require(nordiska_pdf_v1_max_json_bytes() == 32 * 1024 * 1024, "Negative env value must fallback to default");

        custom_max.set("   ");
        require(nordiska_pdf_v1_max_json_bytes() == 32 * 1024 * 1024, "Whitespace env value must fallback to default");

        custom_max.set("1024trailing");
        require(nordiska_pdf_v1_max_json_bytes() == 32 * 1024 * 1024, "Trailing garbage must fallback to default");

        custom_max.set("9999999999999999999999999999999999999");
        require(nordiska_pdf_v1_max_json_bytes() == 32 * 1024 * 1024, "Overflow string must fallback to default");
    }
    require(nordiska_pdf_v1_max_json_bytes() == 32 * 1024 * 1024, "Unset env value must restore default 32 MB");
}

void test_multithreaded_concurrency() {
    std::cout << "Testing high-concurrency multi-threaded execution (16 threads)..." << std::endl;
    ScopedEnv signing("NORDISKA_PDF_ENABLE_SIGNING", "0");

    constexpr size_t kNumThreads = 16;
    constexpr size_t kIterationsPerThread = 5;

    std::vector<std::thread> threads;
    std::atomic<bool> all_ok{true};

    for (size_t t = 0; t < kNumThreads; ++t) {
        threads.emplace_back([t, &all_ok]() {
            try {
                const char* engines[] = {"native", "libharu", "cairo"};
                ScopedEnv thread_engine("NORDISKA_PDF_ENGINE", engines[t % 3]);

                for (size_t iter = 0; iter < kIterationsPerThread; ++iter) {
                    // Alternate between valid and invalid batches
                    if (iter % 2 == 0) {
                        std::string json = kValidBatch;
                        const auto cust_id = 1000 + t * 10 + iter;
                        const auto pos = json.find("888");
                        json.replace(pos, 3, std::to_string(cust_id));

                        BatchCaptureV2 cap;
                        const int status = nordiska_pdf_v2_generate_customer_batch_instrumented(
                            reinterpret_cast<const uint8_t*>(json.data()), json.size(), v2_success_cb, &cap);

                        if (status != NORDISKA_PDF_OK || cap.call_count != 1 || cap.customer_id != cust_id) {
                            all_ok = false;
                        }
                    } else {
                        // Malformed batch
                        const std::string bad_json = "{\"customer_id\": \"not_int\"}";
                        BatchCaptureV1 cap;
                        const int status = nordiska_pdf_v1_generate_customer_batch(
                            reinterpret_cast<const uint8_t*>(bad_json.data()), bad_json.size(), v1_success_cb, &cap);
                        if (status != NORDISKA_PDF_INVALID_INPUT || cap.call_count != 0) {
                            all_ok = false;
                        }
                    }
                }
            } catch (...) {
                all_ok = false;
            }
        });
    }

    for (auto& th : threads) {
        th.join();
    }

    require(all_ok, "All concurrent operations across 16 threads must succeed without race conditions");
}

} // namespace

int main() {
    std::cout << "Starting adversarial C ABI tests..." << std::endl;

    test_boundary_arguments();
    test_callback_rejections_and_hashes();
    test_environment_variable_tampering();
    test_multithreaded_concurrency();

    std::cout << "All adversarial C ABI tests passed successfully!" << std::endl;
    return 0;
}
