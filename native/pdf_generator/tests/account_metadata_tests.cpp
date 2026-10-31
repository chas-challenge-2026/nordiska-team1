#include "nordiska/application/pdf_generator.hpp"
#include "nordiska/c_api/pdf_generator_c_api.h"
#include "pdf_test_helpers.hpp"

#include <cstdlib>
#include <nlohmann/json.hpp>
#include <stdexcept>
#include <string>

namespace {
using namespace nordiska;
void require(bool condition, const char* message) {
    if (!condition) {
        throw std::runtime_error(message);
    }
}
auto bytes(const std::string& text) {
    return std::span(reinterpret_cast<const uint8_t*>(text.data()), text.size());
}
std::string all_text(const DocumentLayout& layout) {
    std::string result;
    for (const auto& page : layout.pages) {
        for (const auto& command : page.commands) {
            if (const auto* text = std::get_if<PositionedText>(&command)) {
                result += text->text + "\n";
            }
        }
    }
    return result;
}
int callbacks{};
int receive_v1(const nordiska_pdf_batch_view*, void*) {
    ++callbacks;
    return 0;
}
int receive_v2(const nordiska_pdf_batch_view_v2*, void*) {
    ++callbacks;
    return 0;
}
} // namespace
int main() {
    ::setenv("NORDISKA_PDF_ENABLE_SIGNING", "0", 1);
    ::setenv("NORDISKA_PDF_ENGINE", "native", 1);
    using json = nlohmann::json;
    json input = {{"customer_id", 42},
                  {"customer_name", "Synthetic customer"},
                  {"documents",
                   {{{"document_id", "statement"},
                     {"kind", "account_statement"},
                     {"document",
                      {{"account_number", "SYNTHETIC-1"},
                       {"account_name", "Min buffert"},
                       {"account_type", "Flex"},
                       {"interest_rate_display", "3,50 %"}}}},
                    {{"document_id", "tax"},
                     {"kind", "annual_tax_report"},
                     {"document",
                      {{"account_number", "SYNTHETIC-2"},
                       {"tax_year", "2026"},
                       {"account_name", "Semester"},
                       {"account_type", "Fixed"},
                       {"interest_rate_display", "4,10 %"}}}}}}};
    for (auto parser : {JsonIngestorKind::Nlohmann, JsonIngestorKind::Simdjson}) {
        JsonIngestor ingestor(parser);
        const auto valid = input.dump();
        const auto parsed = ingestor.ingest(bytes(valid));
        require(parsed.has_value(), "account metadata rejected");
        const auto& statement = std::get<AccountStatement>(parsed->documents[0].content);
        const auto& tax = std::get<AnnualTaxReport>(parsed->documents[1].content);
        require(statement.account_type == "Flex" && statement.interest_rate_display == "3,50 %",
                "statement metadata changed");
        require(tax.account_type == "Fixed" && tax.interest_rate_display == "4,10 %", "tax metadata changed");
        for (const auto& document : parsed->documents) {
            const auto layout = LayoutBuilder::build(document);
            require(layout.has_value(), "metadata layout failed");
            const auto text = all_text(*layout);
            require(text.find("Räntesats: ") != text.npos, "rate label missing");
            require(text.find(document.kind == DocumentKind::AccountStatement ? "Min buffert" : "Semester") !=
                        text.npos,
                    "account nickname was lost");
            require(text.find(document.kind == DocumentKind::AccountStatement ? "Flex" : "Fixed") != text.npos,
                    "product type was lost");
            require(text.find(document.kind == DocumentKind::AccountStatement ? "3,50 %" : "4,10 %") != text.npos,
                    "formatted rate was changed");
            for (auto engine : {PdfEngineKind::Native, PdfEngineKind::Libharu, PdfEngineKind::Cairo}) {
                const auto pdf = PdfEngine(engine).render(*layout);
                require(pdf.has_value() && nordiska::test::validate_pdf_with_ghostscript(*pdf),
                        "metadata PDF is invalid");
            }
        }
        // Missing and empty values retain old inputs and do not invent a rate.
        for (bool absent : {true, false}) {
            auto legacy = input;
            for (auto& document : legacy["documents"]) {
                for (const char* field : {"account_type", "interest_rate_display"}) {
                    if (absent) {
                        document["document"].erase(field);
                    } else {
                        document["document"][field] = "";
                    }
                }
            }
            const auto text = legacy.dump();
            const auto result = ingestor.ingest(bytes(text));
            require(result.has_value(), "legacy metadata payload rejected");
            for (const auto& document : result->documents) {
                const auto layout = LayoutBuilder::build(document);
                require(all_text(*layout).find("Räntesats") == std::string::npos,
                        "missing rate must not become a displayed default");
            }
        }
        // Both optional fields are strictly typed when supplied, on both report
        // types. Only fixed field names and positions may enter diagnostics.
        for (size_t index : {0, 1}) {
            for (const char* field : {"account_type", "interest_rate_display"}) {
                for (const auto& value : std::vector<json>{nullptr, 3.5, true, json::array({"PRIVATE_METADATA"}),
                                                           json{{"PRIVATE_METADATA", "PRIVATE_METADATA"}}}) {
                    auto invalid = input;
                    invalid["documents"][index]["document"][field] = value;
                    const auto text = invalid.dump();
                    const auto result = ingestor.ingest(bytes(text));
                    require(!result && result.error().code == DiagnosticCode::ExpectedString,
                            "invalid metadata type was accepted");
                    const auto expected = std::string_view(field) == "account_type"
                                              ? DiagnosticField::AccountType
                                              : DiagnosticField::InterestRateDisplay;
                    require(result.error().location.document_index == index &&
                                result.error().location.field == expected,
                            "metadata error lost its field or document position");
                    const auto message =
                        format_diagnostic({result.error().code, DiagnosticStage::Ingestion, result.error().location});
                    require(message.view().find("PRIVATE_METADATA") == message.view().npos,
                            "metadata value escaped diagnostic");
                    for (auto call : {nordiska_pdf_v1_generate_customer_batch,
                                      nordiska_pdf_v1_generate_customer_batch_instrumented}) {
                        require(call(bytes(text).data(), text.size(), receive_v1, nullptr) == 2,
                                "V1 metadata rejection status changed");
                    }
                    for (auto call : {nordiska_pdf_v2_generate_customer_batch,
                                      nordiska_pdf_v2_generate_customer_batch_instrumented}) {
                        require(call(bytes(text).data(), text.size(), receive_v2, nullptr) == 2,
                                "V2 metadata rejection status changed");
                    }
                    require(std::string_view(nordiska_pdf_v1_get_last_error()).find("PRIVATE_METADATA") ==
                                std::string_view::npos,
                            "metadata value escaped ABI diagnostics");
                }
            }
        }
    }
    require(callbacks == 0, "invalid metadata delivered partial reports");
    // Long product labels and rate strings follow normal measured wrapping.
    auto long_input = input;
    for (auto& document : long_input["documents"]) {
        document["document"]["account_type"] = std::string(1000, 'P');
        document["document"]["interest_rate_display"] = std::string(1000, '3');
    }
    const auto long_text = long_input.dump();
    PdfGenerator generator({.engine = PdfEngineKind::Native});
    require(generator.generate(bytes(long_text)).has_value(), "long metadata failed generation");
}
