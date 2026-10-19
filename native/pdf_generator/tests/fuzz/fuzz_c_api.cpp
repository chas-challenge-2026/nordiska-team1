#include "nordiska/c_api/pdf_generator_c_api.h"

#include <algorithm>
#include <array>
#include <atomic>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <openssl/sha.h>
#include <string>
#include <thread>
#include <vector>

namespace {

void require(bool condition, const char* message) {
    if (!condition) {
        std::fprintf(stderr, "Contract failure in fuzz_c_api: %s\n", message);
        std::abort();
    }
}

std::string compute_sha256_hex(const uint8_t* data, size_t len) {
    uint8_t hash[SHA256_DIGEST_LENGTH];
    SHA256(data, len, hash);
    constexpr char hex[] = "0123456789abcdef";
    std::string hex_str;
    hex_str.reserve(64);
    for (int i = 0; i < SHA256_DIGEST_LENGTH; ++i) {
        hex_str.push_back(hex[hash[i] >> 4]);
        hex_str.push_back(hex[hash[i] & 0x0F]);
    }
    return hex_str;
}

std::string get_golden_sample_json() {
    const std::vector<std::string> paths = {
        "docs/golden_customer_batch_sample.json",
        "../docs/golden_customer_batch_sample.json",
        "../../docs/golden_customer_batch_sample.json",
        "tests/fuzz/seeds/mixed-batch.json"
    };
    for (const auto& path : paths) {
        if (std::filesystem::exists(path)) {
            std::ifstream f(path);
            if (f.is_open()) {
                return std::string((std::istreambuf_iterator<char>(f)), std::istreambuf_iterator<char>());
            }
        }
    }
    return "{\n  \"customer_id\": 1,\n  \"schema_version\": \"1.0\",\n  \"documents\": [\n    {\n      \"document_id\": \"doc1\",\n      \"kind\": \"account_statement\",\n      \"document\": {\n        \"account_number\": \"1234567890\"\n      }\n    }\n  ]\n}";
}

// 1. Test argument boundary conditions
void test_c_api_boundaries() {
    const std::string golden = get_golden_sample_json();
    const auto* json_bytes = reinterpret_cast<const uint8_t*>(golden.data());
    const size_t json_len = golden.size();

    auto dummy_v1_cb = [](const struct nordiska_pdf_batch_view*, void*) -> int { return 0; };
    auto dummy_v2_cb = [](const struct nordiska_pdf_batch_view_v2*, void*) -> int { return 0; };

    // Null pointer
    require(nordiska_pdf_v1_generate_customer_batch(nullptr, json_len, dummy_v1_cb, nullptr) == 1,
            "must reject null json_utf8 in v1");
    require(nordiska_pdf_v2_generate_customer_batch(nullptr, json_len, dummy_v2_cb, nullptr) == 1,
            "must reject null json_utf8 in v2");

    // Zero length
    require(nordiska_pdf_v1_generate_customer_batch(json_bytes, 0, dummy_v1_cb, nullptr) == 1,
            "must reject zero json_length in v1");
    require(nordiska_pdf_v2_generate_customer_batch(json_bytes, 0, dummy_v2_cb, nullptr) == 1,
            "must reject zero json_length in v2");

    // Null callback
    require(nordiska_pdf_v1_generate_customer_batch(json_bytes, json_len, nullptr, nullptr) == 1,
            "must reject null callback in v1");
    require(nordiska_pdf_v2_generate_customer_batch(json_bytes, json_len, nullptr, nullptr) == 1,
            "must reject null callback in v2");

    // Max bytes helper
    size_t max_bytes = nordiska_pdf_v1_max_json_bytes();
    require(max_bytes == 32 * 1024 * 1024, "max bytes must be 32 MB");

    // Exceeding max bytes limit
    require(nordiska_pdf_v1_generate_customer_batch(json_bytes, max_bytes + 1, dummy_v1_cb, nullptr) == 5,
            "must return NORDISKA_PDF_RESOURCE_LIMIT_EXCEEDED when exceeding max payload");

    // Status name
    for (int st = -2; st < 15; ++st) {
        const char* name = nordiska_pdf_v1_status_name(st);
        require(name != nullptr && std::strlen(name) > 0, "status_name must never return null or empty");
    }
}

// 2. Test V1 and V2 generation with SHA-256 verification and timing
void test_c_api_execution_and_hashes() {
    const std::string golden = get_golden_sample_json();
    const auto* json_bytes = reinterpret_cast<const uint8_t*>(golden.data());
    const size_t json_len = golden.size();

    // V1 delivery
    bool v1_called = false;
    auto v1_cb = [](const struct nordiska_pdf_batch_view* batch, void* udata) -> int {
        auto* called = static_cast<bool*>(udata);
        *called = true;
        require(batch != nullptr, "batch must not be null");
        require(batch->document_count > 0, "document_count must be > 0");
        for (size_t i = 0; i < batch->document_count; ++i) {
            require(batch->documents[i].document_id != nullptr, "document_id not null");
            require(batch->documents[i].bytes != nullptr, "bytes not null");
            require(batch->documents[i].length > 0, "length > 0");
        }
        return 0;
    };

    int rc_v1 = nordiska_pdf_v1_generate_customer_batch_instrumented(json_bytes, json_len, v1_cb, &v1_called);
    require(rc_v1 == 0, "v1 generate instrumented must succeed");
    require(v1_called, "v1 callback must have been called");

    struct nordiska_pdf_pipeline_timing timing{};
    nordiska_pdf_v1_get_last_timing(&timing);
    require(timing.total_seconds > 0.0, "v1 timing total_seconds must be > 0");

    // V2 delivery + Independent SHA-256 validation
    bool v2_called = false;
    auto v2_cb = [](const struct nordiska_pdf_batch_view_v2* batch, void* udata) -> int {
        auto* called = static_cast<bool*>(udata);
        *called = true;
        require(batch != nullptr, "batch must not be null");
        require(batch->document_count > 0, "document_count must be > 0");
        for (size_t i = 0; i < batch->document_count; ++i) {
            const auto& doc = batch->documents[i];
            require(doc.sha256_hex != nullptr, "sha256_hex not null");
            require(std::strlen(doc.sha256_hex) == 64, "sha256_hex must be exactly 64 chars");
            std::string expected_hash = compute_sha256_hex(doc.bytes, doc.length);
            require(expected_hash == doc.sha256_hex, "V2 sha256_hex must match OpenSSL SHA-256 of delivered bytes");
        }
        return 0;
    };

    int rc_v2 = nordiska_pdf_v2_generate_customer_batch_instrumented(json_bytes, json_len, v2_cb, &v2_called);
    require(rc_v2 == 0, "v2 generate instrumented must succeed");
    require(v2_called, "v2 callback must have been called");
}

// 3. Test callback rejection handling
void test_c_api_callback_rejection() {
    const std::string golden = get_golden_sample_json();
    const auto* json_bytes = reinterpret_cast<const uint8_t*>(golden.data());
    const size_t json_len = golden.size();

    auto reject_cb = [](const struct nordiska_pdf_batch_view*, void*) -> int { return 42; };
    int rc = nordiska_pdf_v1_generate_customer_batch(json_bytes, json_len, reject_cb, nullptr);
    require(rc == 3, "rejected callback must return NORDISKA_PDF_CALLBACK_FAILED (3)");

    const char* last_err = nordiska_pdf_v1_get_last_error();
    require(last_err != nullptr && std::strstr(last_err, "42") != nullptr, "last_error must record callback rejection code");
}

// 4. Test engine environment variable selection
void test_c_api_environment_engines() {
    const std::string golden = get_golden_sample_json();
    const auto* json_bytes = reinterpret_cast<const uint8_t*>(golden.data());
    const size_t json_len = golden.size();
    auto dummy_cb = [](const struct nordiska_pdf_batch_view*, void*) -> int { return 0; };

    const std::vector<const char*> valid_engines = {"libharu", "haru", "cairo", "native", "NATIVE", "Cairo"};
    for (const char* eng : valid_engines) {
        ::setenv("NORDISKA_PDF_ENGINE", eng, 1);
        int rc = nordiska_pdf_v1_generate_customer_batch(json_bytes, json_len, dummy_cb, nullptr);
        require(rc == 0, "valid engine environment must succeed");
    }

    // Invalid engine
    ::setenv("NORDISKA_PDF_ENGINE", "invalid_unknown_engine", 1);
    int rc_bad = nordiska_pdf_v1_generate_customer_batch(json_bytes, json_len, dummy_cb, nullptr);
    require(rc_bad == 1, "invalid engine must return NORDISKA_PDF_INVALID_ARGUMENT (1)");
    ::unsetenv("NORDISKA_PDF_ENGINE");
}

// 5. Multithreaded concurrency stress test
void test_c_api_multithreaded_stress() {
    const std::string golden = get_golden_sample_json();
    std::atomic<bool> failed{false};
    std::vector<std::thread> threads;
    constexpr size_t num_threads = 8;
    constexpr size_t calls_per_thread = 20;

    for (size_t t = 0; t < num_threads; ++t) {
        threads.emplace_back([&golden, &failed, t]() {
            const auto* json_bytes = reinterpret_cast<const uint8_t*>(golden.data());
            const size_t json_len = golden.size();
            auto cb = [](const struct nordiska_pdf_batch_view* batch, void*) -> int {
                return (batch && batch->document_count > 0) ? 0 : 1;
            };

            for (size_t c = 0; c < calls_per_thread && !failed.load(); ++c) {
                int rc = nordiska_pdf_v1_generate_customer_batch(json_bytes, json_len, cb, nullptr);
                if (rc != 0) {
                    failed.store(true);
                }
            }
        });
    }

    for (auto& th : threads) {
        th.join();
    }
    require(!failed.load(), "Multithreaded C ABI execution must have zero failures");
}

} // namespace

int main() {
    std::printf("[*] Starting fuzz_c_api...\n");

    std::printf("  -> Testing C API boundary conditions & argument checks...\n");
    test_c_api_boundaries();

    std::printf("  -> Testing V1 and V2 generation + SHA-256 integrity...\n");
    test_c_api_execution_and_hashes();

    std::printf("  -> Testing callback rejection handling...\n");
    test_c_api_callback_rejection();

    std::printf("  -> Testing NORDISKA_PDF_ENGINE environment variable switching...\n");
    test_c_api_environment_engines();

    std::printf("  -> Running 8-thread concurrent stress test...\n");
    test_c_api_multithreaded_stress();

    std::printf("[+] fuzz_c_api completed successfully with 0 crashes!\n");
    return 0;
}

