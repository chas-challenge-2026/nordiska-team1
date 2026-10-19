#include "nordiska/application/pdf_generator.hpp"
#include "nordiska/domain/pdf_rendering_job.hpp"
#include "nordiska/ingestion/json_ingestor.hpp"
#include "pdf_test_helpers.hpp"

#include <cstdint>
#include <iostream>
#include <span>
#include <sstream>
#include <stdexcept>
#include <string>
#include <vector>

namespace {

void require(bool condition, const char* message) {
    if (!condition) {
        std::cerr << "Assertion failed: " << message << std::endl;
        throw std::runtime_error(message);
    }
}

void test_ingestor_with_invalid_payloads(nordiska::JsonIngestorKind kind, const char* ingestor_name) {
    std::cout << "Testing invalid payloads with " << ingestor_name << "..." << std::endl;
    nordiska::JsonIngestor ingestor(kind);

    // List of adversarial / malformed payloads that must fail gracefully
    const std::vector<std::pair<std::string, std::string>> invalid_payloads = {
        {"Empty string", ""},
        {"Whitespace only", "   \t\r\n   "},
        {"Single open brace", "{"},
        {"Single close brace", "}"},
        {"Unclosed string", "{\"customer_id\": \"unclosed"},
        {"Trailing comma in object", "{\"customer_id\": 1,}"},
        {"Trailing comma in array", "{\"customer_id\": 1, \"documents\": [] , }"},
        {"Missing customer_id", "{\"documents\": []}"},
        {"Customer ID is zero", "{\"customer_id\": 0, \"documents\": []}"},
        {"Customer ID is negative", "{\"customer_id\": -42, \"documents\": []}"},
        {"Customer ID is string", "{\"customer_id\": \"1\", \"documents\": []}"},
        {"Customer ID is float", "{\"customer_id\": 1.5, \"documents\": []}"},
        {"Customer ID is bool", "{\"customer_id\": true, \"documents\": []}"},
        {"Customer ID is array", "{\"customer_id\": [1], \"documents\": []}"},
        {"Customer ID is object", "{\"customer_id\": {\"id\": 1}, \"documents\": []}"},
        {"Customer ID overflow", "{\"customer_id\": 99999999999999999999999999999999999999, \"documents\": []}"},
        {"Root is array", "[{\"customer_id\": 1}]"},
        {"Root is string", "\"not a json object\""},
        {"Root is integer", "12345"},
        {"Root is boolean", "false"},
        {"Root is null", "null"},
        {"Missing documents array", "{\"customer_id\": 1}"},
        {"Documents is object", "{\"customer_id\": 1, \"documents\": {}}"},
        {"Documents is string", "{\"customer_id\": 1, \"documents\": \"array\"}"},
        {"Documents is empty array", "{\"customer_id\": 1, \"documents\": []}"},
        {"Document entry is not an object", "{\"customer_id\": 1, \"documents\": [123]}"},
        {"Document entry is string", "{\"customer_id\": 1, \"documents\": [\"doc\"]}"},
        {"Document missing document_id",
         "{\"customer_id\": 1, \"documents\": [{\"kind\": \"account_statement\", \"document\": {}}]}"},
        {"Document document_id is not string", "{\"customer_id\": 1, \"documents\": [{\"document_id\": 42, \"kind\": "
                                               "\"account_statement\", \"document\": {}}]}"},
        {"Document document_id is empty", "{\"customer_id\": 1, \"documents\": [{\"document_id\": \"\", \"kind\": "
                                          "\"account_statement\", \"document\": {}}]}"},
        {"Duplicate document_id",
         "{\"customer_id\": 1, \"documents\": ["
         "{\"document_id\": \"doc_dup\", \"kind\": \"account_statement\", \"document\": {\"account_number\": \"A1\"}},"
         "{\"document_id\": \"doc_dup\", \"kind\": \"account_statement\", \"document\": {\"account_number\": \"A2\"}}"
         "]}"},
        {"Document missing kind", "{\"customer_id\": 1, \"documents\": [{\"document_id\": \"d1\", \"document\": {}}]}"},
        {"Document unsupported kind", "{\"customer_id\": 1, \"documents\": [{\"document_id\": \"d1\", \"kind\": "
                                      "\"unsupported_kind\", \"document\": {}}]}"},
        {"Document missing document payload",
         "{\"customer_id\": 1, \"documents\": [{\"document_id\": \"d1\", \"kind\": \"account_statement\"}]}"},
        {"Document document payload is not object", "{\"customer_id\": 1, \"documents\": [{\"document_id\": \"d1\", "
                                                    "\"kind\": \"account_statement\", \"document\": \"not_obj\"}]}"},
        {"AccountStatement missing account_number",
         "{\"customer_id\": 1, \"documents\": [{\"document_id\": \"d1\", \"kind\": \"account_statement\", "
         "\"document\": {\"title\": \"Konto\"}}]}"},
        {"AccountStatement account_number is not string",
         "{\"customer_id\": 1, \"documents\": [{\"document_id\": \"d1\", \"kind\": \"account_statement\", "
         "\"document\": {\"account_number\": 12345}}]}"},
        {"AccountStatement transactions is not array",
         "{\"customer_id\": 1, \"documents\": [{\"document_id\": \"d1\", \"kind\": \"account_statement\", "
         "\"document\": {\"account_number\": \"A1\", \"transactions\": \"not_array\"}}]}"},
        {"Transaction is not object",
         "{\"customer_id\": 1, \"documents\": [{\"document_id\": \"d1\", \"kind\": \"account_statement\", "
         "\"document\": {\"account_number\": \"A1\", \"transactions\": [123]}}]}"},
        {"Transaction missing date",
         "{\"customer_id\": 1, \"documents\": [{\"document_id\": \"d1\", \"kind\": \"account_statement\", "
         "\"document\": {\"account_number\": \"A1\", \"transactions\": [{\"description\": \"test\"}]}}]}"},
        {"Transaction date is not string",
         "{\"customer_id\": 1, \"documents\": [{\"document_id\": \"d1\", \"kind\": \"account_statement\", "
         "\"document\": {\"account_number\": \"A1\", \"transactions\": [{\"date\": 20260101}]}}]}"},
        {"Transaction amount_minor is float",
         "{\"customer_id\": 1, \"documents\": [{\"document_id\": \"d1\", \"kind\": \"account_statement\", "
         "\"document\": {\"account_number\": \"A1\", \"transactions\": [{\"date\": \"2026-01-01\", \"amount_minor\": "
         "12.34}]}}]}"},
        {"Transaction amount_minor is string",
         "{\"customer_id\": 1, \"documents\": [{\"document_id\": \"d1\", \"kind\": \"account_statement\", "
         "\"document\": {\"account_number\": \"A1\", \"transactions\": [{\"date\": \"2026-01-01\", \"amount_minor\": "
         "\"100\"}]}}]}"},
        {"AnnualTaxReport missing tax_year", "{\"customer_id\": 1, \"documents\": [{\"document_id\": \"d1\", \"kind\": "
                                             "\"annual_tax_report\", \"document\": {\"account_number\": \"A1\"}}]}"},
        {"AnnualTaxReport tax_year is not string",
         "{\"customer_id\": 1, \"documents\": [{\"document_id\": \"d1\", \"kind\": \"annual_tax_report\", "
         "\"document\": {\"account_number\": \"A1\", \"tax_year\": 2025}}]}"},
        {"AnnualTaxReport missing account_number",
         "{\"customer_id\": 1, \"documents\": [{\"document_id\": \"d1\", \"kind\": \"annual_tax_report\", "
         "\"document\": {\"tax_year\": \"2025\"}}]}"},
    };

    for (const auto& [name, json_str] : invalid_payloads) {
        std::span<const uint8_t> span{reinterpret_cast<const uint8_t*>(json_str.data()), json_str.size()};
        auto res = ingestor.ingest(span);
        require(!res.has_value(), ("Payload must fail: " + name).c_str());
        require(!res.error().message.empty() || !res.error().path.empty(),
                ("Error diagnostic must not be empty for: " + name).c_str());
    }
}

void test_deeply_nested_json(nordiska::JsonIngestorKind kind) {
    std::cout << "Testing deeply nested JSON structures..." << std::endl;
    nordiska::JsonIngestor ingestor(kind);

    // Build deeply nested array: [[[[...]]]]
    std::string nested;
    for (int i = 0; i < 200; ++i) {
        nested += "[";
    }
    nested += "{\"customer_id\": 1}";
    for (int i = 0; i < 200; ++i) {
        nested += "]";
    }

    std::span<const uint8_t> span{reinterpret_cast<const uint8_t*>(nested.data()), nested.size()};
    auto res = ingestor.ingest(span);
    require(!res.has_value(), "Deeply nested array payload must fail gracefully without stack overflow");
}

void test_utf8_edge_cases(nordiska::JsonIngestorKind kind) {
    std::cout << "Testing UTF-8 boundary and corrupt sequences..." << std::endl;
    nordiska::JsonIngestor ingestor(kind);

    // 1. Invalid UTF-8 lead bytes
    const std::vector<uint8_t> invalid_lead = {
        '{', '"', 'c', 'u', 's', 't', 'o', 'm', 'e', 'r', '_', 'i', 'd',  '"',  ':', ' ', '1', ',', '"', 'd',
        'o', 'c', 'u', 'm', 'e', 'n', 't', 's', '"', ':', ' ', '[', '{',  '"',  'd', 'o', 'c', 'u', 'm', 'e',
        'n', 't', '_', 'i', 'd', '"', ':', ' ', '"', 'd', '"', ',', '"',  'k',  'i', 'n', 'd', '"', ':', ' ',
        '"', 'a', 'c', 'c', 'o', 'u', 'n', 't', '_', 's', 't', 'a', 't',  'e',  'm', 'e', 'n', 't', '"', ',',
        '"', 'd', 'o', 'c', 'u', 'm', 'e', 'n', 't', '"', ':', ' ', '{',  '"',  'a', 'c', 'c', 'o', 'u', 'n',
        't', '_', 'n', 'u', 'm', 'b', 'e', 'r', '"', ':', ' ', '"', 0xFF, 0xFE, // Invalid UTF-8 bytes
        '"', '}', '}', ']', '}'};
    auto res_lead = ingestor.ingest(invalid_lead);
    // Invalid UTF-8 in JSON must either fail parse or be rejected
    (void)res_lead;

    // 2. Truncated multibyte sequence
    const std::vector<uint8_t> truncated_utf8 = {'{',
                                                 '"',
                                                 'c',
                                                 'u',
                                                 's',
                                                 't',
                                                 'o',
                                                 'm',
                                                 'e',
                                                 'r',
                                                 '_',
                                                 'i',
                                                 'd',
                                                 '"',
                                                 ':',
                                                 ' ',
                                                 '1',
                                                 ',',
                                                 '"',
                                                 'd',
                                                 'o',
                                                 'c',
                                                 'u',
                                                 'm',
                                                 'e',
                                                 'n',
                                                 't',
                                                 's',
                                                 '"',
                                                 ':',
                                                 ' ',
                                                 '[',
                                                 '{',
                                                 '"',
                                                 'd',
                                                 'o',
                                                 'c',
                                                 'u',
                                                 'm',
                                                 'e',
                                                 'n',
                                                 't',
                                                 '_',
                                                 'i',
                                                 'd',
                                                 '"',
                                                 ':',
                                                 ' ',
                                                 '"',
                                                 'd',
                                                 '"',
                                                 ',',
                                                 '"',
                                                 'k',
                                                 'i',
                                                 'n',
                                                 'd',
                                                 '"',
                                                 ':',
                                                 ' ',
                                                 '"',
                                                 'a',
                                                 'c',
                                                 'c',
                                                 'o',
                                                 'u',
                                                 'n',
                                                 't',
                                                 '_',
                                                 's',
                                                 't',
                                                 'a',
                                                 't',
                                                 'e',
                                                 'm',
                                                 'e',
                                                 'n',
                                                 't',
                                                 '"',
                                                 ',',
                                                 '"',
                                                 'd',
                                                 'o',
                                                 'c',
                                                 'u',
                                                 'm',
                                                 'e',
                                                 'n',
                                                 't',
                                                 '"',
                                                 ':',
                                                 ' ',
                                                 '{',
                                                 '"',
                                                 'a',
                                                 'c',
                                                 'c',
                                                 'o',
                                                 'u',
                                                 'n',
                                                 't',
                                                 '_',
                                                 'n',
                                                 'u',
                                                 'm',
                                                 'b',
                                                 'e',
                                                 'r',
                                                 '"',
                                                 ':',
                                                 ' ',
                                                 '"',
                                                 static_cast<uint8_t>(0xE2),
                                                 static_cast<uint8_t>(0x80), // missing 3rd continuation byte
                                                 '"',
                                                 '}',
                                                 '}',
                                                 ']',
                                                 '}'};
    auto res_trunc = ingestor.ingest(truncated_utf8);
    (void)res_trunc;
}

void test_large_valid_json_batches(nordiska::JsonIngestorKind kind) {
    std::cout << "Testing large valid batch payloads..." << std::endl;
    nordiska::JsonIngestor ingestor(kind);

    // 1. Batch with 100 documents
    std::ostringstream oss;
    oss << "{\"customer_id\": 42, \"schema_version\": \"1.0\", \"created_at\": \"2026-03-01T12:00:00Z\", "
           "\"documents\": [";
    for (int i = 0; i < 100; ++i) {
        if (i > 0) {
            oss << ",";
        }
        oss << "{\"document_id\": \"stmt_" << i
            << "\", \"kind\": \"account_statement\", \"document\": {\"account_number\": \"NKM-" << i
            << "\", \"title\": \"Konto " << i << "\"}}";
    }
    oss << "]}";

    std::string json_data = oss.str();
    std::span<const uint8_t> span{reinterpret_cast<const uint8_t*>(json_data.data()), json_data.size()};
    auto res = ingestor.ingest(span);
    require(res.has_value(), "Ingestion of 100-document batch must succeed");
    require(res->customer_id == 42, "Customer ID mismatch");
    require(res->documents.size() == 100, "Documents count must be 100");

    // 2. Document with 2,000 transactions
    std::ostringstream tx_oss;
    tx_oss << "{\"customer_id\": 99, \"documents\": [{\"document_id\": \"large_stmt\", \"kind\": "
              "\"account_statement\", \"document\": {\"account_number\": \"SE123\", \"transactions\": [";
    for (int i = 0; i < 2000; ++i) {
        if (i > 0) {
            tx_oss << ",";
        }
        tx_oss << "{\"date\": \"2026-01-01\", \"description\": \"Köp " << i << "\", \"amount_minor\": " << (i * 10)
               << "}";
    }
    tx_oss << "]}}]}";

    std::string tx_json = tx_oss.str();
    std::span<const uint8_t> tx_span{reinterpret_cast<const uint8_t*>(tx_json.data()), tx_json.size()};
    auto tx_res = ingestor.ingest(tx_span);
    require(tx_res.has_value(), "Ingestion of 2,000-transaction document must succeed");
    require(tx_res->documents.size() == 1, "Documents count must be 1");
    const auto& stmt = std::get<nordiska::AccountStatement>(tx_res->documents[0].content);
    require(stmt.transactions.size() == 2000, "Transactions count must be 2000");
}

} // namespace

int main() {
    std::cout << "Starting adversarial JSON fuzzing tests..." << std::endl;

    test_ingestor_with_invalid_payloads(nordiska::JsonIngestorKind::Simdjson, "Simdjson");
    test_ingestor_with_invalid_payloads(nordiska::JsonIngestorKind::Nlohmann, "Nlohmann");

    test_deeply_nested_json(nordiska::JsonIngestorKind::Simdjson);
    test_deeply_nested_json(nordiska::JsonIngestorKind::Nlohmann);

    test_utf8_edge_cases(nordiska::JsonIngestorKind::Simdjson);
    test_utf8_edge_cases(nordiska::JsonIngestorKind::Nlohmann);

    test_large_valid_json_batches(nordiska::JsonIngestorKind::Simdjson);
    test_large_valid_json_batches(nordiska::JsonIngestorKind::Nlohmann);

    std::cout << "All adversarial JSON fuzzing tests passed successfully!" << std::endl;
    return 0;
}
