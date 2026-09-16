#include "nordiska/application/pdf_generator.hpp"
#include "nordiska/domain/document_types.hpp"
#include "nordiska/domain/pdf_rendering_job.hpp"
#include "nordiska/ingestion/json_ingestor.hpp"

#include <cstring>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <stdexcept>
#include <string>
#include <variant>

namespace {

void require(bool condition, const char* message) {
    if (!condition) {
        throw std::runtime_error(message);
    }
}

} // namespace

int main() {
    nordiska::JsonIngestor ingestor;

    // 1. Ingestion of golden_customer_batch_sample.json
    std::filesystem::path golden_path = "docs/golden_customer_batch_sample.json";
    if (!std::filesystem::exists(golden_path)) {
        golden_path = "../docs/golden_customer_batch_sample.json";
    }
    require(std::filesystem::exists(golden_path), "golden sample file must exist");

    std::ifstream file(golden_path);
    require(file.is_open(), "could not open golden sample file");
    std::string golden_json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());

    std::span<const uint8_t> golden_span{reinterpret_cast<const uint8_t*>(golden_json.data()), golden_json.size()};
    auto golden_res = ingestor.ingest(golden_span);
    require(golden_res.has_value(), "golden sample ingestion should succeed");

    const nordiska::PdfRenderingJob& job = *golden_res;
    require(job.customer_id == 1, "customer_id mismatch");
    require(job.customer_name == "Anna Lindqvist", "customer_name mismatch");
    require(job.schema_version == "1.0", "schema_version mismatch");
    require(job.created_at == "2026-02-01T10:00:00Z", "created_at mismatch");
    require(job.documents.size() == 2, "job must contain 2 documents");

    // Document 0: AccountStatement
    const nordiska::Document& doc0 = job.documents[0];
    require(doc0.document_id == "account1_statement", "doc0 id mismatch");
    require(doc0.kind == nordiska::DocumentKind::AccountStatement, "doc0 kind mismatch");
    require(std::holds_alternative<nordiska::AccountStatement>(doc0.content), "doc0 content type mismatch");
    const auto& statement = std::get<nordiska::AccountStatement>(doc0.content);
    require(statement.title == "Kontoutdrag", "statement title mismatch");
    require(statement.account_number == "NKM-10001", "statement account_number mismatch");
    require(statement.account_name == "Sparkonto", "statement account_name mismatch");
    require(statement.currency == "SEK", "statement currency mismatch");
    require(statement.period == "2026-01-01 - 2026-01-31", "statement period mismatch");
    require(statement.opening_balance == "90 000,00 SEK", "opening_balance mismatch");
    require(statement.closing_balance == "125 000,00 SEK", "closing_balance mismatch");
    require(statement.transactions.size() == 1, "transactions count mismatch");
    require(statement.transactions[0].date == "2026-01-15", "transaction date mismatch");
    require(statement.transactions[0].type == "deposit", "transaction type mismatch");
    require(statement.transactions[0].description == "Löneinsättning", "transaction description mismatch");
    require(statement.transactions[0].amount_minor == 3500000, "transaction amount_minor mismatch");
    require(statement.transactions[0].amount_display == "+35 000,00 SEK", "amount_display mismatch");
    require(statement.transactions[0].balance_after_display == "125 000,00 SEK", "balance_after_display mismatch");

    // Document 1: AnnualTaxReport
    const nordiska::Document& doc1 = job.documents[1];
    require(doc1.document_id == "account1_tax", "doc1 id mismatch");
    require(doc1.kind == nordiska::DocumentKind::AnnualTaxReport, "doc1 kind mismatch");
    require(std::holds_alternative<nordiska::AnnualTaxReport>(doc1.content), "doc1 content type mismatch");
    const auto& tax_report = std::get<nordiska::AnnualTaxReport>(doc1.content);
    require(tax_report.title == "Kontrolluppgift för ränteinkomst", "tax title mismatch");
    require(tax_report.tax_year == "2025", "tax_year mismatch");
    require(tax_report.account_number == "NKM-10001", "tax account_number mismatch");
    require(tax_report.account_name == "Sparkonto", "tax account_name mismatch");
    require(tax_report.total_interest_earned == "4 375,00 SEK", "total_interest_earned mismatch");
    require(tax_report.preliminary_tax_deducted == "1 312,50 SEK", "preliminary_tax_deducted mismatch");
    require(tax_report.reported_to_authority == "Skatteverket (KU20)", "reported_to_authority mismatch");

    // 2. Duplicate document_id rejection
    const std::string dup_json = R"({
        "customer_id": 1,
        "documents": [
            {
                "document_id": "duplicate_id",
                "kind": "account_statement",
                "document": { "account_number": "NKM-1" }
            },
            {
                "document_id": "duplicate_id",
                "kind": "account_statement",
                "document": { "account_number": "NKM-2" }
            }
        ]
    })";
    auto dup_res =
        ingestor.ingest(std::span<const uint8_t>{reinterpret_cast<const uint8_t*>(dup_json.data()), dup_json.size()});
    require(!dup_res.has_value(), "duplicate document_id must fail");
    require(dup_res.error().message.find("Duplicate document_id: duplicate_id") != std::string::npos,
            "error should indicate duplicate id");

    // 3. Unsupported document kind rejection
    const std::string unknown_kind_json = R"({
        "customer_id": 1,
        "documents": [
            {
                "document_id": "test_id",
                "kind": "unknown_future_kind",
                "document": {}
            }
        ]
    })";
    auto unknown_res = ingestor.ingest(
        std::span<const uint8_t>{reinterpret_cast<const uint8_t*>(unknown_kind_json.data()), unknown_kind_json.size()});
    require(!unknown_res.has_value(), "unknown kind must fail");
    require(unknown_res.error().message.find("unsupported document kind: unknown_future_kind") != std::string::npos,
            "error should mention unsupported kind");

    // 4. Missing required customer_id
    const std::string missing_cust_json = R"({
        "documents": [
            {
                "document_id": "doc1",
                "kind": "account_statement",
                "document": { "account_number": "NKM-1" }
            }
        ]
    })";
    auto missing_cust_res = ingestor.ingest(
        std::span<const uint8_t>{reinterpret_cast<const uint8_t*>(missing_cust_json.data()), missing_cust_json.size()});
    require(!missing_cust_res.has_value(), "missing customer_id must fail");
    require(missing_cust_res.error().message.find("customer_id") != std::string::npos,
            "error should mention customer_id");

    // 5. Zero customer_id
    const std::string zero_cust_json = R"({
        "customer_id": 0,
        "documents": [
            {
                "document_id": "doc1",
                "kind": "account_statement",
                "document": { "account_number": "NKM-1" }
            }
        ]
    })";
    auto zero_cust_res = ingestor.ingest(
        std::span<const uint8_t>{reinterpret_cast<const uint8_t*>(zero_cust_json.data()), zero_cust_json.size()});
    require(!zero_cust_res.has_value(), "zero customer_id must fail");
    require(zero_cust_res.error().message.find("customer_id must be greater than zero") != std::string::npos,
            "error should indicate customer_id > 0");

    // 6. Empty documents array
    const std::string empty_docs_json = R"({
        "customer_id": 1,
        "documents": []
    })";
    auto empty_docs_res = ingestor.ingest(
        std::span<const uint8_t>{reinterpret_cast<const uint8_t*>(empty_docs_json.data()), empty_docs_json.size()});
    require(!empty_docs_res.has_value(), "empty documents array must fail");

    // 7. Missing account_number in account_statement
    const std::string missing_acct_json = R"({
        "customer_id": 1,
        "documents": [
            {
                "document_id": "doc1",
                "kind": "account_statement",
                "document": { "title": "Kontoutdrag" }
            }
        ]
    })";
    auto missing_acct_res = ingestor.ingest(
        std::span<const uint8_t>{reinterpret_cast<const uint8_t*>(missing_acct_json.data()), missing_acct_json.size()});
    require(!missing_acct_res.has_value(), "missing account_number must fail");
    require(missing_acct_res.error().message.find("account_number") != std::string::npos,
            "error should mention account_number");

    // 8. Malformed JSON syntax
    const std::string malformed_json = "{ bad json ";
    auto malformed_res = ingestor.ingest(
        std::span<const uint8_t>{reinterpret_cast<const uint8_t*>(malformed_json.data()), malformed_json.size()});
    require(!malformed_res.has_value(), "malformed json must fail");
    require(malformed_res.error().message.find("Malformed JSON") != std::string::npos,
            "error should indicate malformed json");

    // 9. PdfGenerator with default config parses golden sample
    nordiska::PdfGenerator default_gen{nordiska::GeneratorConfig{}};
    auto default_batch_res = default_gen.generate(golden_span);
    require(default_batch_res.has_value(), "default generator should parse golden sample");
    require(default_batch_res->customer_id == 1, "generated batch customer_id mismatch");
    require(default_batch_res->documents.size() == 2, "generated batch documents count mismatch");

    // 10. PdfGenerator with Simdjson config reports not yet implemented
    nordiska::PdfGenerator simd_gen{nordiska::GeneratorConfig{.ingestor = nordiska::JsonIngestorKind::Simdjson}};
    auto simd_res = simd_gen.generate(golden_span);
    require(!simd_res.has_value(), "simdjson should report not implemented");
    require(simd_res.error().message.find("simdjson") != std::string::npos, "error should mention simdjson");

    std::cout << "All json ingestor tests passed successfully!\n";
    return 0;
}
