#include "nordiska/delivery/c_api/pdf_generator_c_api.h"

#include <cstdint>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <stdexcept>
#include <string>
#include <thread>
#include <vector>

namespace {

void require(bool condition, const char* message) {
    if (!condition) {
        throw std::runtime_error(message);
    }
}

struct BatchCapture {
    uint64_t customer_id{0};
    size_t call_count{0};
    struct DocCopy {
        std::string id;
        std::vector<uint8_t> bytes;
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

} // namespace

int main() {
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
    require(std::strlen(nordiska_pdf_v1_get_last_error()) > 0, "error message must be provided on failure");
    require(std::string(nordiska_pdf_v1_get_last_error()).find("unsupported_unknown_kind") != std::string::npos,
            "error diagnostic must identify unsupported kind");

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
        require(golden_capture.docs[1].id == "account1_tax", "golden doc 1 id mismatch");
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
}
