#include "nordiska/application/pdf_generator.hpp"
#include "nordiska/c_api/pdf_generator_c_api.h"
#include "nordiska/layout/layout_builder.hpp"
#include "nordiska/rendering/pdf_engine.hpp"
#include "nordiska/rendering/utf8_to_cp1252.hpp"
#include "nordiska/signing/pdf_signer.hpp"
#include "pdf_test_helpers.hpp"

#include <algorithm>
#include <array>
#include <cstdint>
#include <cstdlib>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <memory>
#include <openssl/sha.h>
#include <optional>
#include <stdexcept>
#include <string>
#include <thread>
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

void require(bool condition, const char* message) {
    if (!condition) {
        throw std::runtime_error(message);
    }
}

std::array<double, 9> timing_values(const nordiska_pdf_pipeline_timing& timing) {
    return {timing.ingest_seconds, timing.layout_seconds,      timing.render_seconds,
            timing.sign_seconds,   timing.signer_call_seconds, timing.prepare_seconds,
            timing.digest_seconds, timing.checksum_seconds,    timing.total_seconds};
}

nordiska_pdf_pipeline_timing latest_timing() {
    nordiska_pdf_pipeline_timing timing{};
    nordiska_pdf_v1_get_last_timing(&timing);
    return timing;
}

class ScopedEnvironment {
  public:
    ScopedEnvironment(const char* name, const char* value) : name_(name) {
        if (const char* original = std::getenv(name)) {
            original_ = original;
        }
        set(value);
    }
    ~ScopedEnvironment() {
        set(original_ ? original_->c_str() : nullptr);
    }
    void set(const char* value) const {
        if (value) {
            ::setenv(name_, value, 1);
        } else {
            ::unsetenv(name_);
        }
    }

  private:
    const char* name_;
    std::optional<std::string> original_;
};

struct TestSigner : nordiska::PdfSigner {
    std::string failure_document;
    explicit TestSigner(std::string failure = "") : failure_document(std::move(failure)) {}
    std::expected<std::string, nordiska::SigningError>
    sign_digest(std::span<const uint8_t, 32>, const nordiska::SigningContext& context, double*) override {
        if (context.document_id == failure_document) {
            return std::unexpected(nordiska::SigningError{nordiska::SigningErrorKind::SignatureGenerationFailed,
                                                          "Simulated signer failure"});
        }
        return "3000";
    }
};

struct BatchCapture {
    uint64_t customer_id{0};
    size_t call_count{0};
    struct DocCopy {
        std::string id;
        std::vector<uint8_t> bytes;
        std::string sha256_hex;
    };
    std::vector<DocCopy> docs;
};

int batch_save_cb(const struct nordiska_pdf_batch_view* batch, void* user_data) {
    auto* capture = static_cast<BatchCapture*>(user_data);
    capture->call_count++;
    capture->customer_id = batch->customer_id;
    for (size_t i = 0; i < batch->document_count; ++i) {
        const auto& doc = batch->documents[i];
        capture->docs.push_back({
            .id = doc.document_id ? doc.document_id : "",
            .bytes = std::vector<uint8_t>(doc.bytes, doc.bytes + doc.length),
        });
    }
    return 0;
}

int batch_reject_cb(const struct nordiska_pdf_batch_view*, void*) {
    return 42;
}

int batch_save_cb_v2(const struct nordiska_pdf_batch_view_v2* batch, void* user_data) {
    auto* capture = static_cast<BatchCapture*>(user_data);
    ++capture->call_count;
    capture->customer_id = batch->customer_id;
    for (size_t i = 0; i < batch->document_count; ++i) {
        const auto& doc = batch->documents[i];
        require(doc.sha256_hex != nullptr && std::strlen(doc.sha256_hex) == 64, "v2 hash must have 64 characters");
        std::array<unsigned char, SHA256_DIGEST_LENGTH> digest;
        require(SHA256(doc.bytes, doc.length, digest.data()) != nullptr, "independent SHA-256 failed");
        constexpr char hex[] = "0123456789abcdef";
        std::string expected;
        for (auto byte : digest) {
            expected += hex[byte >> 4];
            expected += hex[byte & 0x0f];
        }
        require(expected == doc.sha256_hex, "v2 hash must match the complete callback PDF bytes");
        capture->docs.push_back({
            .id = doc.document_id,
            .bytes = std::vector<uint8_t>(doc.bytes, doc.bytes + doc.length),
            .sha256_hex = doc.sha256_hex,
        });
    }
    return 0;
}

int batch_reject_cb_v2(const struct nordiska_pdf_batch_view_v2*, void*) {
    return 42;
}

} // namespace

int main() {
    require(timing_values(latest_timing()) == std::array<double, 9>{}, "new thread must have zero timings");
    nordiska_pdf_v1_get_last_timing(nullptr);
    // 1. Status name helper tests (NOR-140)
    require(std::strcmp(nordiska_pdf_v1_status_name(NORDISKA_PDF_OK), "NORDISKA_PDF_OK") == 0,
            "status name OK mismatch");
    require(std::strcmp(nordiska_pdf_v1_status_name(NORDISKA_PDF_INVALID_ARGUMENT), "NORDISKA_PDF_INVALID_ARGUMENT") ==
                0,
            "status name INVALID_ARGUMENT mismatch");
    require(std::strcmp(nordiska_pdf_v1_status_name(NORDISKA_PDF_INVALID_INPUT), "NORDISKA_PDF_INVALID_INPUT") == 0,
            "status name INVALID_INPUT mismatch");
    require(std::strcmp(nordiska_pdf_v1_status_name(NORDISKA_PDF_CALLBACK_FAILED), "NORDISKA_PDF_CALLBACK_FAILED") == 0,
            "status name CALLBACK_FAILED mismatch");
    require(std::strcmp(nordiska_pdf_v1_status_name(NORDISKA_PDF_INTERNAL_ERROR), "NORDISKA_PDF_INTERNAL_ERROR") == 0,
            "status name INTERNAL_ERROR mismatch");
    require(std::strcmp(nordiska_pdf_v1_status_name(NORDISKA_PDF_RESOURCE_LIMIT_EXCEEDED),
                        "NORDISKA_PDF_RESOURCE_LIMIT_EXCEEDED") == 0,
            "status name RESOURCE_LIMIT_EXCEEDED mismatch");
    require(std::strcmp(nordiska_pdf_v1_status_name(NORDISKA_PDF_OUT_OF_MEMORY), "NORDISKA_PDF_OUT_OF_MEMORY") == 0,
            "status name OUT_OF_MEMORY mismatch");
    require(std::strcmp(nordiska_pdf_v1_status_name(NORDISKA_PDF_SIGNING_FAILED), "NORDISKA_PDF_SIGNING_FAILED") == 0,
            "status name SIGNING_FAILED mismatch");
    require(std::strcmp(nordiska_pdf_v1_status_name(999), "status code does not exist") == 0,
            "status name unknown mismatch");

    // 2. Granular argument validation tests (NOR-144)
    require(nordiska_pdf_v1_generate_customer_batch(nullptr, 10, batch_save_cb, nullptr) ==
                NORDISKA_PDF_INVALID_ARGUMENT,
            "null json must return invalid argument");
    require(std::string(nordiska_pdf_v1_get_last_error()).find("json_utf8 must not be null") != std::string::npos,
            "error should mention json_utf8");

    require(nordiska_pdf_v1_generate_customer_batch(reinterpret_cast<const uint8_t*>("{}"), 0, batch_save_cb,
                                                    nullptr) == NORDISKA_PDF_INVALID_ARGUMENT,
            "zero length must return invalid argument");
    require(std::string(nordiska_pdf_v1_get_last_error()).find("json_length must be greater than zero") !=
                std::string::npos,
            "error should mention json_length");

    require(nordiska_pdf_v1_generate_customer_batch(reinterpret_cast<const uint8_t*>("{}"), 2, nullptr, nullptr) ==
                NORDISKA_PDF_INVALID_ARGUMENT,
            "null callback must return invalid argument");
    require(std::string(nordiska_pdf_v1_get_last_error()).find("callback must not be null") != std::string::npos,
            "error should mention callback");

    // 3. Happy path: batch with account_statement and annual_tax_report (NOR-141)
    const std::string valid_batch_json = R"({
        "customer_id": 1,
        "documents": [
            {
                "document_id": "account1_statement",
                "kind": "account_statement",
                "document": {
                    "account_number": "NKM-10001",
                    "title": "Kontoutdrag",
                    "period": "2026-01-01 - 2026-01-31",
                    "closing_balance": "125 000,00 SEK",
                    "transactions": [
                        {
                            "date": "2026-01-15",
                            "description": "Insättning",
                            "currency": "SEK",
                            "amount_minor": 100000
                        }
                    ]
                }
            },
            {
                "document_id": "account1_tax",
                "kind": "annual_tax_report",
                "document": {
                    "account_number": "NKM-10001",
                    "tax_year": "2025",
                    "total_interest_earned": "4 375,00 SEK",
                    "preliminary_tax_deducted": "1 312,50 SEK"
                }
            }
        ]
    })";

    BatchCapture capture;
    const int batch_ok = nordiska_pdf_v1_generate_customer_batch(
        reinterpret_cast<const uint8_t*>(valid_batch_json.data()), valid_batch_json.size(), batch_save_cb, &capture);
    require(batch_ok == NORDISKA_PDF_OK, nordiska_pdf_v1_get_last_error());
    require(std::strlen(nordiska_pdf_v1_get_last_error()) == 0, "last error must be empty on success");
    require(capture.call_count == 1, "callback should be invoked exactly once on success");
    require(capture.customer_id == 1, "batch customer_id mismatch");
    require(capture.docs.size() == 2, "batch must deliver 2 documents");
    require(capture.docs[0].id == "account1_statement", "doc 0 id mismatch");
    require(capture.docs[0].bytes.size() > 8, "doc 0 empty bytes");
    require(std::memcmp(capture.docs[0].bytes.data(), "%PDF-1.3", 8) == 0, "doc 0 not a PDF-1.3");
    require(capture.docs[1].id == "account1_tax", "doc 1 id mismatch");
    require(capture.docs[1].bytes.size() > 8, "doc 1 empty bytes");
    require(std::memcmp(capture.docs[1].bytes.data(), "%PDF-1.3", 8) == 0, "doc 1 not a PDF-1.3");

    // #223: select the renderer without rebuilding; legacy defaults stay Libharu.
    {
        ScopedEnvironment signing("NORDISKA_PDF_ENABLE_SIGNING", "0");
        ScopedEnvironment engine_env("NORDISKA_PDF_ENGINE", nullptr);
        const std::span<const uint8_t> payload(reinterpret_cast<const uint8_t*>(valid_batch_json.data()),
                                               valid_batch_json.size());
        for (const auto& [name, engine] :
             std::vector<std::pair<const char*, nordiska::PdfEngineKind>>{{nullptr, nordiska::PdfEngineKind::Libharu},
                                                                          {"", nordiska::PdfEngineKind::Libharu},
                                                                          {"libharu", nordiska::PdfEngineKind::Libharu},
                                                                          {"HaRu", nordiska::PdfEngineKind::Libharu},
                                                                          {"NaTiVe", nordiska::PdfEngineKind::Native},
                                                                          {"CAIRO", nordiska::PdfEngineKind::Cairo}}) {
            engine_env.set(name);
            BatchCapture selected;
            require(nordiska_pdf_v1_generate_customer_batch(payload.data(), payload.size(), batch_save_cb, &selected) ==
                        NORDISKA_PDF_OK,
                    nordiska_pdf_v1_get_last_error());
            nordiska::PdfGenerator expected(nordiska::GeneratorConfig{.engine = engine});
            auto generated = expected.generate(payload);
            require(generated.has_value(), "direct renderer generation failed");
            require(selected.call_count == 1 && selected.docs.size() == generated->documents.size(),
                    "selected renderer must deliver the full customer batch");
            for (size_t i = 0; i < selected.docs.size(); ++i) {
                require(
                    selected.docs[i].id == generated->documents[i].document_id &&
                        nordiska::test::same_rendered_pdf(selected.docs[i].bytes, generated->documents[i].pdf_bytes),
                    "C API output must match the selected renderer, including Swedish text and geometry");
            }
        }
        engine_env.set("typo");
        BatchCapture rejected;
        require(nordiska_pdf_v1_generate_customer_batch(payload.data(), payload.size(), batch_save_cb, &rejected) ==
                        NORDISKA_PDF_INVALID_ARGUMENT &&
                    rejected.call_count == 0,
                "unknown engine must fail before delivery");
        require(std::string(nordiska_pdf_v1_get_last_error()).find("NORDISKA_PDF_ENGINE") != std::string::npos,
                "unknown engine diagnostic must identify the setting");
    }

    // #220: v2 adds artifact hashes while leaving v1 callbacks and layouts intact.
    {
        ScopedEnvironment signing("NORDISKA_PDF_ENABLE_SIGNING", "0");
        ScopedEnvironment engine("NORDISKA_PDF_ENGINE", "haru");
        const auto* data = reinterpret_cast<const uint8_t*>(valid_batch_json.data());
        require(nordiska_pdf_v2_generate_customer_batch(nullptr, 1, batch_save_cb_v2, nullptr) ==
                    NORDISKA_PDF_INVALID_ARGUMENT,
                "v2 must reject null input");
        require(nordiska_pdf_v2_generate_customer_batch(data, 0, batch_save_cb_v2, nullptr) ==
                    NORDISKA_PDF_INVALID_ARGUMENT,
                "v2 must reject zero input length");
        require(nordiska_pdf_v2_generate_customer_batch(data, valid_batch_json.size(), nullptr, nullptr) ==
                    NORDISKA_PDF_INVALID_ARGUMENT,
                "v2 must reject null callback");
        require(nordiska_pdf_v2_generate_customer_batch(data, nordiska_pdf_v1_max_json_bytes() + 1, batch_save_cb_v2,
                                                        nullptr) == NORDISKA_PDF_RESOURCE_LIMIT_EXCEEDED,
                "v2 must enforce the existing payload limit before reading input");
        require(nordiska_pdf_v2_generate_customer_batch(data, valid_batch_json.size(), batch_reject_cb_v2, nullptr) ==
                    NORDISKA_PDF_CALLBACK_FAILED,
                "v2 must report callback rejection");
        require(std::string(nordiska_pdf_v1_get_last_error()).find("42") != std::string::npos,
                "v2 must share thread-local diagnostics");
        for (const char* selected : {"haru", "native", "cairo"}) {
            engine.set(selected);
            BatchCapture v1, v2;
            require(nordiska_pdf_v1_generate_customer_batch(data, valid_batch_json.size(), batch_save_cb, &v1) == 0,
                    "v1 compatibility generation failed");
            require(nordiska_pdf_v2_generate_customer_batch(data, valid_batch_json.size(), batch_save_cb_v2, &v2) == 0,
                    nordiska_pdf_v1_get_last_error());
            require(std::strlen(nordiska_pdf_v1_get_last_error()) == 0, "v2 success must clear prior diagnostic");
            require(v2.customer_id == v1.customer_id && v2.call_count == 1 && v2.docs.size() == v1.docs.size(),
                    "v2 must preserve batch delivery semantics");
            for (size_t i = 0; i < v2.docs.size(); ++i) {
                require(v2.docs[i].id == v1.docs[i].id &&
                            nordiska::test::same_rendered_pdf(v2.docs[i].bytes, v1.docs[i].bytes),
                        "v2 must preserve v1 document output");
                require(v2.docs[i].sha256_hex.size() == 64, "copied hash must remain valid after return");
            }
            require(v2.docs[0].sha256_hex != v2.docs[1].sha256_hex, "different PDFs must expose their own hashes");
        }
    }

    // 4. All-or-nothing guarantee: document 2 fails -> 0 callbacks invoked
    const std::string failing_batch_json = R"({
        "customer_id": 1,
        "documents": [
            {
                "document_id": "account1_statement",
                "kind": "account_statement",
                "document": {
                    "account_number": "NKM-10001",
                    "title": "Kontoutdrag",
                    "transactions": [
                        {
                            "date": "2026-01-15",
                            "description": "Insättning",
                            "currency": "SEK",
                            "amount_minor": 100000
                        }
                    ]
                }
            },
            {
                "document_id": "broken_doc",
                "kind": "unsupported_unknown_kind",
                "document": {}
            }
        ]
    })";

    BatchCapture fail_capture;
    const int batch_fail =
        nordiska_pdf_v1_generate_customer_batch(reinterpret_cast<const uint8_t*>(failing_batch_json.data()),
                                                failing_batch_json.size(), batch_save_cb, &fail_capture);
    require(batch_fail == NORDISKA_PDF_INVALID_INPUT, "unsupported kind should return invalid-input");
    require(fail_capture.call_count == 0, "callback MUST NOT be called on failure");
    BatchCapture fail_capture_v2;
    require(nordiska_pdf_v2_generate_customer_batch(reinterpret_cast<const uint8_t*>(failing_batch_json.data()),
                                                    failing_batch_json.size(), batch_save_cb_v2,
                                                    &fail_capture_v2) == NORDISKA_PDF_INVALID_INPUT &&
                fail_capture_v2.call_count == 0,
            "v2 must preserve all-or-nothing delivery on invalid second document");
    require(std::strlen(nordiska_pdf_v1_get_last_error()) > 0, "error message must be provided on failure");
    require(std::string(nordiska_pdf_v1_get_last_error()).find("unsupported_unknown_kind") != std::string::npos,
            "error diagnostic must identify unsupported kind");

    // Stateless v1 and v2 calls must isolate customers during concurrent use.
    {
        ScopedEnvironment signing("NORDISKA_PDF_ENABLE_SIGNING", "0");
        ScopedEnvironment engine("NORDISKA_PDF_ENGINE", "native");
        std::array<std::array<BatchCapture, 3>, 2> batches;
        std::array<std::array<int, 3>, 2> statuses;
        std::array<std::thread, 2> workers;
        for (size_t version = 0; version < workers.size(); ++version) {
            workers[version] = std::thread([&, version] {
                for (size_t call = 0; call < batches[version].size(); ++call) {
                    std::string input = valid_batch_json;
                    const auto customer = 100 * (version + 1) + call;
                    const auto start = input.find("\"customer_id\": 1");
                    input.replace(start, std::strlen("\"customer_id\": 1"),
                                  "\"customer_id\": " + std::to_string(customer));
                    const auto* data = reinterpret_cast<const uint8_t*>(input.data());
                    auto& batch = batches[version][call];
                    statuses[version][call] =
                        version == 0
                            ? nordiska_pdf_v1_generate_customer_batch(data, input.size(), batch_save_cb, &batch)
                            : nordiska_pdf_v2_generate_customer_batch(data, input.size(), batch_save_cb_v2, &batch);
                    std::fill(input.begin(), input.end(), 'x');
                }
            });
        }
        for (auto& worker : workers) {
            worker.join();
        }
        for (size_t version = 0; version < batches.size(); ++version) {
            for (size_t call = 0; call < batches[version].size(); ++call) {
                const auto& batch = batches[version][call];
                require(statuses[version][call] == NORDISKA_PDF_OK && batch.call_count == 1 &&
                            batch.customer_id == 100 * (version + 1) + call && batch.docs.size() == 2,
                        "stateless calls must deliver separate customer batches");
                require(batch.docs[0].bytes.size() > 8 && std::memcmp(batch.docs[0].bytes.data(), "%PDF-1.4", 8) == 0,
                        "copied PDF output must outlive each stateless call and its input");
            }
        }
    }

    // 5. Duplicate document_id rejected
    const std::string dup_batch_json = R"({
        "customer_id": 1,
        "documents": [
            {
                "document_id": "same_id",
                "kind": "account_statement",
                "document": {
                    "account_number": "NKM-10001",
                    "transactions": [
                        { "date": "2026-01-15", "description": "T1", "currency": "SEK", "amount_minor": 100 }
                    ]
                }
            },
            {
                "document_id": "same_id",
                "kind": "account_statement",
                "document": {
                    "account_number": "NKM-10002",
                    "transactions": [
                        { "date": "2026-01-15", "description": "T2", "currency": "SEK", "amount_minor": 200 }
                    ]
                }
            }
        ]
    })";
    const int dup_status = nordiska_pdf_v1_generate_customer_batch(
        reinterpret_cast<const uint8_t*>(dup_batch_json.data()), dup_batch_json.size(), batch_save_cb, &fail_capture);
    require(dup_status == NORDISKA_PDF_INVALID_INPUT, "duplicate document_id must fail");
    require(std::string(nordiska_pdf_v1_get_last_error()).find("Duplicate document_id") != std::string::npos,
            "error should mention duplicate document_id");

    // 6. Callback rejection with status code check (NOR-143)
    const int reject_status = nordiska_pdf_v1_generate_customer_batch(
        reinterpret_cast<const uint8_t*>(valid_batch_json.data()), valid_batch_json.size(), batch_reject_cb, nullptr);
    require(reject_status == NORDISKA_PDF_CALLBACK_FAILED, "rejected callback must return callback-failed");
    require(std::string(nordiska_pdf_v1_get_last_error()).find("42") != std::string::npos,
            "error message should contain callback status code 42");

    // 7. Resource limit query functions (NOR-157)
    require(nordiska_pdf_v1_max_json_bytes() == 32 * 1024 * 1024, "max json bytes mismatch");

    // 8. Ground-truth golden sample verification
    std::filesystem::path golden_path = "docs/golden_customer_batch_sample.json";
    if (!std::filesystem::exists(golden_path)) {
        golden_path = "../docs/golden_customer_batch_sample.json";
    }
    if (!std::filesystem::exists(golden_path)) {
        golden_path = "../../docs/golden_customer_batch_sample.json";
    }
    if (std::filesystem::exists(golden_path)) {
        std::ifstream file(golden_path);
        require(file.is_open(), "could not open golden sample file");
        std::string golden_json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
        BatchCapture golden_capture;
        const int golden_ok = nordiska_pdf_v1_generate_customer_batch(
            reinterpret_cast<const uint8_t*>(golden_json.data()), golden_json.size(), batch_save_cb, &golden_capture);
        require(golden_ok == NORDISKA_PDF_OK, nordiska_pdf_v1_get_last_error());
        require(std::strlen(nordiska_pdf_v1_get_last_error()) == 0,
                "last error must be empty after golden sample success");
        require(golden_capture.call_count == 1, "golden callback invoked once");
        require(golden_capture.customer_id == 1, "golden customer_id mismatch");
        require(golden_capture.docs.size() == 2, "golden docs count mismatch");
        require(golden_capture.docs[0].id == "account1_statement", "golden doc 0 id mismatch");
        require(golden_capture.docs[0].bytes.size() > 500, "golden doc 0 must be real PDF bytes");
        require(std::string_view(reinterpret_cast<const char*>(golden_capture.docs[0].bytes.data()), 5) == "%PDF-",
                "golden doc 0 header must be %PDF-");
        require(golden_capture.docs[1].id == "account1_tax", "golden doc 1 id mismatch");
        require(golden_capture.docs[1].bytes.size() > 500, "golden doc 1 must be real PDF bytes");
        require(std::string_view(reinterpret_cast<const char*>(golden_capture.docs[1].bytes.data()), 5) == "%PDF-",
                "golden doc 1 header must be %PDF-");

        require(timing_values(latest_timing()) == std::array<double, 9>{},
                "normal generation must not collect timings");
    }

    // Both ABI versions share normal/instrumented generation and delivery semantics.
    {
        ScopedEnvironment signing("NORDISKA_PDF_ENABLE_SIGNING", "0");
        ScopedEnvironment engine("NORDISKA_PDF_ENGINE", "native");
        const auto* data = reinterpret_cast<const uint8_t*>(valid_batch_json.data());
        const auto exercise = [&](auto normal, auto instrumented, auto callback, auto reject) {
            BatchCapture empty;
            require(instrumented(nullptr, 1, callback, &empty) == NORDISKA_PDF_INVALID_ARGUMENT &&
                        empty.call_count == 0 && timing_values(latest_timing()) == std::array<double, 9>{},
                    "instrumented argument failure must replace old measurements with zeros");
            BatchCapture ordinary, profiled;
            require(normal(data, valid_batch_json.size(), callback, &ordinary) == NORDISKA_PDF_OK,
                    "normal generation failed");
            require(timing_values(latest_timing()) == std::array<double, 9>{},
                    "normal call must leave the timing snapshot untouched");
            require(instrumented(data, valid_batch_json.size(), callback, &profiled) == NORDISKA_PDF_OK,
                    nordiska_pdf_v1_get_last_error());
            require(profiled.call_count == 1 && profiled.customer_id == ordinary.customer_id &&
                        profiled.docs.size() == ordinary.docs.size(),
                    "instrumentation must preserve atomic batch delivery");
            for (size_t i = 0; i < ordinary.docs.size(); ++i) {
                require(ordinary.docs[i].id == profiled.docs[i].id &&
                            nordiska::test::same_rendered_pdf(ordinary.docs[i].bytes, profiled.docs[i].bytes),
                        "normal and instrumented calls must produce equivalent PDFs");
            }
            const auto measured = latest_timing();
            require(measured.ingest_seconds > 0.0 && measured.layout_seconds > 0.0 && measured.render_seconds > 0.0 &&
                        measured.checksum_seconds > 0.0,
                    "instrumented call must record pipeline phases");
            require(measured.prepare_seconds == 0.0 && measured.digest_seconds == 0.0 &&
                        measured.signer_call_seconds == 0.0,
                    "unsigned instrumentation must not record signing work");
            require(measured.total_seconds == measured.ingest_seconds + measured.layout_seconds +
                                                  measured.render_seconds + measured.sign_seconds,
                    "total must sum the native pipeline phases");
            require(timing_values(latest_timing()) == timing_values(measured),
                    "getter must copy the snapshot without consuming it");
            BatchCapture subsequent;
            require(normal(data, valid_batch_json.size(), callback, &subsequent) == NORDISKA_PDF_OK,
                    "subsequent normal call failed");
            require(normal(nullptr, 1, callback, &empty) == NORDISKA_PDF_INVALID_ARGUMENT,
                    "normal argument validation changed");
            require(timing_values(latest_timing()) == timing_values(measured),
                    "normal successes and failures must preserve the latest instrumented snapshot");
            require(instrumented(data, valid_batch_json.size(), reject, nullptr) == NORDISKA_PDF_CALLBACK_FAILED &&
                        std::string(nordiska_pdf_v1_get_last_error()).find("42") != std::string::npos,
                    "instrumentation must preserve callback rejection diagnostics");
            require(latest_timing().render_seconds > 0.0, "callback rejection must retain recorded phases");
            BatchCapture failed;
            require(instrumented(reinterpret_cast<const uint8_t*>(failing_batch_json.data()), failing_batch_json.size(),
                                 callback, &failed) == NORDISKA_PDF_INVALID_INPUT &&
                        failed.call_count == 0,
                    "instrumented invalid batches must invoke no callbacks");
            const auto partial = latest_timing();
            require(partial.ingest_seconds > 0.0 && partial.render_seconds == 0.0 && partial.checksum_seconds == 0.0,
                    "failed instrumented calls must publish their own partial work without stale later phases");
        };
        for (const char* selected : {"haru", "native", "cairo"}) {
            engine.set(selected);
            exercise(nordiska_pdf_v1_generate_customer_batch, nordiska_pdf_v1_generate_customer_batch_instrumented,
                     batch_save_cb, batch_reject_cb);
            exercise(nordiska_pdf_v2_generate_customer_batch, nordiska_pdf_v2_generate_customer_batch_instrumented,
                     batch_save_cb_v2, batch_reject_cb_v2);
        }

        const auto main_snapshot = timing_values(latest_timing());
        bool isolated = false;
        std::thread worker([&] {
            if (timing_values(latest_timing()) != std::array<double, 9>{}) {
                return;
            }
            BatchCapture capture;
            isolated = nordiska_pdf_v2_generate_customer_batch_instrumented(
                           data, valid_batch_json.size(), batch_save_cb_v2, &capture) == NORDISKA_PDF_OK &&
                       latest_timing().render_seconds > 0.0;
        });
        worker.join();
        require(isolated && timing_values(latest_timing()) == main_snapshot,
                "instrumented snapshots must stay isolated between threads");

        // A nested instrumented callback must not overwrite its parent's collector.
        engine.set("native");
        const auto nested_callback = +[](const nordiska_pdf_batch_view*, void*) -> int {
            const int status =
                nordiska_pdf_v2_generate_customer_batch_instrumented(nullptr, 1, batch_save_cb_v2, nullptr);
            return status == NORDISKA_PDF_INVALID_ARGUMENT && timing_values(latest_timing()) == std::array<double, 9>{}
                       ? 42
                       : 43;
        };
        require(nordiska_pdf_v1_generate_customer_batch_instrumented(data, valid_batch_json.size(), nested_callback,
                                                                     nullptr) == NORDISKA_PDF_CALLBACK_FAILED &&
                    std::string(nordiska_pdf_v1_get_last_error()).find("42") != std::string::npos,
                "outer call must preserve callback rejection after nested instrumentation");
        require(latest_timing().render_seconds > 0.0 && latest_timing().checksum_seconds > 0.0,
                "latest completed outer call must publish its own measurements after the nested call");
    }

    // 9. Thread-local isolation test for nordiska_pdf_v1_get_last_error
    // Main thread triggers an argument error
    nordiska_pdf_v1_generate_customer_batch(nullptr, 0, nullptr, nullptr);
    const std::string main_err = nordiska_pdf_v1_get_last_error();
    require(!main_err.empty(), "main thread must have recorded error");

    bool thread_tested_ok = false;
    std::thread worker_thread([&]() {
        // Worker thread must initially see empty string, not main thread's error
        if (std::strlen(nordiska_pdf_v1_get_last_error()) != 0) {
            return;
        }
        // Worker triggers its own different error
        nordiska_pdf_v1_generate_customer_batch(reinterpret_cast<const uint8_t*>("{\"bad\":true}"), 12, batch_save_cb,
                                                nullptr);
        const std::string worker_err = nordiska_pdf_v1_get_last_error();
        if (worker_err.find("customer_id") != std::string::npos) {
            thread_tested_ok = true;
        }
    });
    worker_thread.join();

    require(thread_tested_ok, "worker thread isolation test failed");
    // Main thread's last error must still be untouched
    require(nordiska_pdf_v1_get_last_error() == main_err,
            "main thread last error must not be clobbered by worker thread");

    // 10. Step 7: Signing Seam happy path with timing
    {
        const nordiska::GeneratorConfig sign_cfg{
            .ingestor = nordiska::JsonIngestorKind::Simdjson,
            .engine = nordiska::PdfEngineKind::Native,
            .enable_signing = true,
            .compression = true,
            .custom_signer = std::make_shared<TestSigner>(),
        };
        nordiska::PdfGenerator sign_gen(sign_cfg);
        nordiska::PipelineTiming timing;
        const std::span<const uint8_t> valid_span{reinterpret_cast<const uint8_t*>(valid_batch_json.data()),
                                                  valid_batch_json.size()};
        auto res = sign_gen.generate(valid_span, &timing);
        require(res.has_value(), "signing-enabled generation must succeed with stub signer");
        require(res->documents.size() == 2, "batch must contain 2 signed documents");
        require(timing.sign_seconds >= 0.0, "sign timing must be recorded");
        require(timing.total_seconds() >= timing.sign_seconds, "total timing must include sign timing");
    }

    // 11. Step 7: All-or-Nothing Signing Failure Guarantee
    {
        auto mock_signer = std::make_shared<TestSigner>("account1_tax");
        const nordiska::GeneratorConfig fail_cfg{
            .ingestor = nordiska::JsonIngestorKind::Simdjson,
            .engine = nordiska::PdfEngineKind::Native,
            .enable_signing = true,
            .compression = true,
            .custom_signer = mock_signer,
        };
        nordiska::PdfGenerator fail_gen(fail_cfg);
        const std::span<const uint8_t> valid_span{reinterpret_cast<const uint8_t*>(valid_batch_json.data()),
                                                  valid_batch_json.size()};
        auto fail_res = fail_gen.generate(valid_span);
        require(!fail_res.has_value(), "signing failure on doc 2 must fail the entire batch");
        require(std::holds_alternative<nordiska::SigningError>(fail_res.error().details),
                "error details must be SigningError");
        require(fail_res.error().message().find("Failed to sign document 'account1_tax'") != std::string::npos,
                "error message must identify failed document");
    }

    // 12. Step 7: SigningContext customer & document ID propagation
    {
        struct ContextCheckingSigner : public nordiska::PdfSigner {
            std::vector<std::string> seen_docs;
            std::vector<uint64_t> seen_customers;

            std::expected<std::string, nordiska::SigningError>
            sign_digest(std::span<const uint8_t, 32>, const nordiska::SigningContext& context, double*) override {
                seen_docs.emplace_back(context.document_id);
                seen_customers.push_back(context.customer_id);
                return "3000";
            }
        };

        auto ctx_signer = std::make_shared<ContextCheckingSigner>();
        const nordiska::GeneratorConfig ctx_cfg{
            .ingestor = nordiska::JsonIngestorKind::Simdjson,
            .engine = nordiska::PdfEngineKind::Native,
            .enable_signing = true,
            .compression = true,
            .custom_signer = ctx_signer,
        };
        nordiska::PdfGenerator ctx_gen(ctx_cfg);
        const std::span<const uint8_t> valid_span{reinterpret_cast<const uint8_t*>(valid_batch_json.data()),
                                                  valid_batch_json.size()};
        auto ctx_res = ctx_gen.generate(valid_span);
        require(ctx_res.has_value(), "context checking generation must succeed");
        require(ctx_signer->seen_docs.size() == 2, "must see 2 documents");
        require(ctx_signer->seen_docs[0] == "account1_statement", "doc 0 must be account1_statement");
        require(ctx_signer->seen_docs[1] == "account1_tax", "doc 1 must be account1_tax");
        require(ctx_signer->seen_customers[0] == 1 && ctx_signer->seen_customers[1] == 1,
                "customer_id must be 1 for all documents");
    }

    // 13. Step NOR-206: LayoutBuilder std::expected return and error handling
    {
        // 13.1 Success paths
        nordiska::AccountStatement stmt;
        stmt.account_number = "NKM-99999";
        stmt.title = "Test Statement";
        auto stmt_res = nordiska::LayoutBuilder::build_statement(stmt);
        require(stmt_res.has_value(), "build_statement must succeed for valid statement");
        require(!stmt_res->pages.empty(), "statement layout must produce at least one page");

        nordiska::AnnualTaxReport tax;
        tax.account_number = "NKM-99999";
        tax.tax_year = "2025";
        auto tax_res = nordiska::LayoutBuilder::build_tax_report(tax);
        require(tax_res.has_value(), "build_tax_report must succeed for valid tax report");
        require(!tax_res->pages.empty(), "tax report layout must produce at least one page");

        nordiska::Document doc{
            .document_id = "test_doc",
            .content = stmt,
        };
        auto doc_res = nordiska::LayoutBuilder::build(doc);
        require(doc_res.has_value(), "build must succeed for valid Document containing statement");

        // 13.2 Multi-page statement succeeds naturally without artificial page limits
        nordiska::AccountStatement multi_page_stmt;
        multi_page_stmt.account_number = "NKM-MULTIPAGE";
        multi_page_stmt.transactions.resize(150, nordiska::StatementTransaction{
                                                     .date = "2026-01-01",
                                                     .type = "Köp",
                                                     .description = "Test transaction",
                                                     .amount_display = "-10,00 SEK",
                                                     .balance_after_display = "1 000,00 SEK",
                                                 });
        auto multi_res = nordiska::LayoutBuilder::build_statement(multi_page_stmt);
        require(multi_res.has_value(), "multi-page statement must succeed without arbitrary page limit");
        require(multi_res->pages.size() > 1, "150 transactions must produce multiple pages");

        // 13.3 Error types
        const nordiska::LayoutError err{
            .kind = nordiska::LayoutErrorKind::UnsupportedDocumentType,
            .message = "Unsupported document content type",
        };
        require(err.kind == nordiska::LayoutErrorKind::UnsupportedDocumentType,
                "LayoutErrorKind should match UnsupportedDocumentType");
        require(err.message == "Unsupported document content type", "LayoutError message mismatch");
    }

    // 14. Step NOR-214: UTF-8 to CP1252 conversion, Unicode minus (U+2212), typography and single-? fallback
    {
        // 14.1 Unicode minus sign (U+2212) maps to ASCII '-'
        std::string amount_cp1252;
        nordiska::utf8_to_cp1252_append("−645,50 SEK", amount_cp1252);
        require(amount_cp1252 == "-645,50 SEK", "Unicode minus must map to ASCII '-' in CP1252");
        require(amount_cp1252.find("???") == std::string::npos, "Must not emit ??? for Unicode minus");

        // 14.2 PDF escaped string conversion handles Unicode minus and special characters
        std::string pdf_escaped;
        nordiska::append_pdf_escaped_text(pdf_escaped, "−645,50 SEK (konto\\valuta)");
        require(pdf_escaped == "-645,50 SEK \\(konto\\\\valuta\\)",
                "append_pdf_escaped_text must escape '(' ')' '\\' while mapping Unicode minus");

        // 14.3 Swedish characters and Euro symbol continue to render cleanly
        std::string swedish_text;
        nordiska::utf8_to_cp1252_append("Åke & Älva på Österlen: 100 €", swedish_text);
        require(swedish_text.find("???") == std::string::npos, "Swedish characters and Euro must not emit ???");
        require(swedish_text.find('\x80') != std::string::npos, "Euro symbol must map to 0x80 in CP1252");

        // 14.4 Swedish thousands separator (narrow no-break space U+202F) maps to space
        std::string grouped_amount;
        nordiska::utf8_to_cp1252_append("1\xE2\x80\xAF"
                                        "234\xE2\x80\xAF"
                                        "567,89 SEK",
                                        grouped_amount);
        require(grouped_amount == "1 234 567,89 SEK", "Narrow no-break space (U+202F) must map to space, not ???");

        // 14.5 En-dash (U+2013), Swedish quotes (U+201D), and ellipsis (U+2026)
        std::string typography;
        nordiska::utf8_to_cp1252_append("Period: 2025\xE2\x80\x93"
                                        "2026 \xE2\x80\x9D"
                                        "Nordiska\xE2\x80\x9D \xE2\x80\xA6",
                                        typography);
        require(typography.find("???") == std::string::npos, "Typography must not emit ???");
        require(typography.find('\x96') != std::string::npos, "En-dash must map to 0x96 in CP1252");
        require(typography.find('\x94') != std::string::npos, "Right double quote must map to 0x94 in CP1252");
        require(typography.find('\x85') != std::string::npos, "Ellipsis must map to 0x85 in CP1252");

        // 14.6 Invisible formatting characters (BOM U+FEFF, ZWS U+200B) are safely dropped
        std::string invisible_stripped;
        nordiska::utf8_to_cp1252_append("BOM\xEF\xBB\xBF"
                                        "Test\xE2\x80\x8B"
                                        "End",
                                        invisible_stripped);
        require(invisible_stripped == "BOMTestEnd", "Invisible formatting characters must be dropped");

        // 14.7 Unrecognized multi-byte sequences consume continuation bytes and emit single '?'
        std::string fallback_2byte;
        nordiska::utf8_to_cp1252_append("Cyrillic \xD0\x94 letter", fallback_2byte);
        require(fallback_2byte == "Cyrillic ? letter", "Unrecognized 2-byte sequence must emit single '?'");

        std::string fallback_4byte;
        nordiska::utf8_to_cp1252_append("Emoji \xF0\x9F\x98\x80 test", fallback_4byte);
        require(fallback_4byte == "Emoji ? test", "Unrecognized 4-byte sequence must emit single '?'");

        // 14.8 Decomposed Unicode (NFD) combining diacritics fold to precomposed CP1252
        std::string nfd_swedish;
        // "A\u030Ake & A\u0308lva pa\u030A O\u0308sterlen: e\u0301tudiant u\u0308ber"
        nordiska::utf8_to_cp1252_append("A\xCC\x8A"
                                        "ke & A\xCC\x88"
                                        "lva pa\xCC\x8A O\xCC\x88"
                                        "sterlen: e\xCC\x81"
                                        "tudiant u\xCC\x88"
                                        "ber",
                                        nfd_swedish);
        std::string nfc_expected;
        nordiska::utf8_to_cp1252_append("Åke & Älva på Österlen: étudiant über", nfc_expected);
        require(nfd_swedish == nfc_expected, "NFD decomposed sequences must fold identically to NFC precomposed");
        require(nfd_swedish.find('?') == std::string::npos, "NFD decomposed Swedish characters must not emit '?'");

        // 14.9 European transliteration fallbacks (Polish, Czech, German)
        std::string translit;
        // "Wałęsa Dvořák Groß" (with capital sharp S U+1E9E)
        nordiska::utf8_to_cp1252_append("Wa\xC5\x82\xC4\x99sa Dvo\xC5\x99\xC3\xA1k Gro\xE1\xBA\x9E", translit);
        require(translit.find("Wal") != std::string::npos, "Ł (U+0142) should transliterate to 'l'");
        require(translit.find("Dvor") != std::string::npos, "ř (U+0159) should transliterate to 'r'");
        require(translit.find('\xDF') != std::string::npos, "Capital sharp S (U+1E9E) should map to ß (0xDF)");

        // 14.10 End-to-end rendering in Native and Libharu engines with Unicode minus
        nordiska::AccountStatement stmt;
        stmt.account_number = "SE1234567890";
        stmt.title = "Kontoutdrag";
        stmt.transactions.push_back(nordiska::StatementTransaction{
            .date = "2026-01-20",
            .type = "Uttag",
            .description = "Uttag bankomat",
            .amount_display = "−645,50 SEK",
            .balance_after_display = "10 000,00 SEK",
        });

        auto layout_res = nordiska::LayoutBuilder::build_statement(stmt);
        require(layout_res.has_value(), "Statement layout build must succeed");

        // Native engine (uncompressed stream verification)
        nordiska::PdfEngine native_engine(nordiska::PdfEngineKind::Native, false);
        auto native_pdf_res = native_engine.render(*layout_res);
        require(native_pdf_res.has_value(), "Native engine render must succeed");
        std::string native_pdf_str(native_pdf_res->begin(), native_pdf_res->end());
        require(native_pdf_str.find("(-645,50 SEK) Tj") != std::string::npos,
                "Native PDF output must contain '(-645,50 SEK) Tj'");
        require(native_pdf_str.find("???") == std::string::npos, "Native PDF output must not contain ???");

        // Libharu engine (uncompressed stream verification)
        nordiska::PdfEngine haru_engine(nordiska::PdfEngineKind::Libharu, false);
        auto haru_pdf_res = haru_engine.render(*layout_res);
        require(haru_pdf_res.has_value(), "Haru engine render must succeed");
        std::string haru_pdf_str(haru_pdf_res->begin(), haru_pdf_res->end());
        require(haru_pdf_str.find("-645,50 SEK") != std::string::npos, "Libharu PDF output must contain '-645,50 SEK'");
        require(haru_pdf_str.find("???") == std::string::npos, "Libharu PDF output must not contain ???");
    }

    // 17. Step NOR-207: std::variant-based error handling in PdfGenerator
    {
        // 17.1 Invalid argument error
        nordiska::GeneratorConfig invalid_arg_cfg{
            .signature_contents_capacity = 3, // odd length is invalid
        };
        nordiska::PdfGenerator invalid_gen(invalid_arg_cfg);
        const std::string dummy_json = "{}";
        const std::span<const uint8_t> dummy_span{reinterpret_cast<const uint8_t*>(dummy_json.data()),
                                                  dummy_json.size()};
        auto arg_res = invalid_gen.generate(dummy_span);
        require(!arg_res.has_value(), "invalid argument configuration must fail");
        require(std::holds_alternative<nordiska::InvalidArgumentError>(arg_res.error().details),
                "error details must hold InvalidArgumentError");
        require(!arg_res.error().message().empty(), "error message must not be empty");

        // 17.2 Ingest error preserves path and kind
        nordiska::GeneratorConfig valid_cfg;
        nordiska::PdfGenerator gen(valid_cfg);
        const std::string malformed_json = R"({"customer_id": "not_an_int"})";
        const std::span<const uint8_t> malformed_span{reinterpret_cast<const uint8_t*>(malformed_json.data()),
                                                      malformed_json.size()};
        auto ingest_fail = gen.generate(malformed_span);
        require(!ingest_fail.has_value(), "malformed json must fail ingestion");
        require(std::holds_alternative<nordiska::IngestError>(ingest_fail.error().details),
                "error details must hold IngestError");
        const auto* ingest_err = std::get_if<nordiska::IngestError>(&ingest_fail.error().details);
        require(ingest_err != nullptr, "get_if must succeed for IngestError");
        require(ingest_err->path == "customer_id", "IngestError path must be preserved without loss");

        // 17.3 Pattern matching / visitor pattern
        bool visited_ingest = false;
        std::visit(
            [&](const auto& err_payload) {
                using T = std::decay_t<decltype(err_payload)>;
                if constexpr (std::is_same_v<T, nordiska::IngestError>) {
                    visited_ingest = true;
                }
            },
            ingest_fail.error().details);
        require(visited_ingest, "std::visit over GeneratorErrorPayload must match IngestError branch");
    }
}
