#include "nordiska/application/pdf_generator.hpp"
#include "nordiska/c_api/pdf_generator_c_api.h"

#include <cstdlib>
#include <iostream>
#include <limits>
#include <nlohmann/json.hpp>
#include <stdexcept>
#include <type_traits>

using namespace nordiska;
namespace {
constexpr std::string_view marker = "PRIVATE_CUSTOMER_918273645";
void require(bool condition, const char* message) {
    if (!condition) {
        throw std::runtime_error(message);
    }
}
std::span<const uint8_t> bytes(const std::string& value) {
    return {reinterpret_cast<const uint8_t*>(value.data()), value.size()};
}
void check_private(std::string_view diagnostic) {
    require(!diagnostic.empty(), "diagnostic must explain failure");
    require(diagnostic.find(marker) == std::string_view::npos, "customer string escaped");
    require(diagnostic.find("918273645") == std::string_view::npos, "customer ID escaped");
}
struct ThrowingSigner : PdfSigner {
    bool out_of_memory{};
    std::expected<std::string, SigningError> sign_digest(std::span<const uint8_t, 32>, double*) override {
        if (out_of_memory) {
            throw std::bad_alloc{};
        }
        throw std::runtime_error(std::string(marker));
    }
};
int callbacks = 0;
int receive_v1(const nordiska_pdf_batch_view*, void*) {
    ++callbacks;
    return 918273645;
}
int receive_v2(const nordiska_pdf_batch_view_v2*, void*) {
    ++callbacks;
    return 918273645;
}
} // namespace

// Compile-time contract: failures have no owning or borrowed text payloads.
static_assert(std::is_trivially_copyable_v<IngestError>);
static_assert(std::is_trivially_copyable_v<LayoutError>);
static_assert(std::is_trivially_copyable_v<RenderError>);
static_assert(std::is_trivially_copyable_v<SigningError>);
static_assert(std::is_trivially_copyable_v<GeneratorError>);
static_assert(!std::is_constructible_v<SigningError, SigningErrorKind, std::string>);
static_assert(!std::is_constructible_v<Diagnostic, std::string_view>);

int main() {
    ::setenv("NORDISKA_PDF_ENABLE_SIGNING", "0", 1);
    ::setenv("NORDISKA_PDF_ENGINE", "native", 1);
    nlohmann::json document = {{"document_id", marker},
                               {"kind", "account_statement"},
                               {"document", {{"account_number", marker}, {"account_name", marker}}}};
    nlohmann::json valid = {{"customer_id", 918273645}, {"customer_name", marker}, {"documents", {document}}};
    std::vector<std::string> inputs;
    auto duplicate = valid;
    duplicate["documents"].push_back(document);
    inputs.push_back(duplicate.dump());
    auto unknown_kind = valid;
    unknown_kind["documents"][0]["kind"] = marker;
    inputs.push_back(unknown_kind.dump());
    auto invalid_field = valid;
    invalid_field["documents"][0]["document"]["account_number"] = nlohmann::json::array({marker});
    inputs.push_back(invalid_field.dump());
    auto invalid_transaction = valid;
    invalid_transaction["documents"][0]["document"]["transactions"] = nlohmann::json::array({{{"date", 918273645}}});
    inputs.push_back(invalid_transaction.dump());
    inputs.push_back("{\"customer_name\":\"" + std::string(marker) + "\",\"broken\":}");
    auto missing = valid;
    missing["documents"][0]["document"].erase("account_number");
    // An unknown key is input too; it must never become a diagnostic path.
    missing["documents"][0]["document"][marker] = marker;
    inputs.push_back(missing.dump());
    for (auto kind : {JsonIngestorKind::Nlohmann, JsonIngestorKind::Simdjson}) {
        JsonIngestor ingestor(kind);
        for (const auto& input : inputs) {
            const auto parsed = ingestor.ingest(bytes(input));
            require(!parsed, "invalid input should fail");
            check_private(
                format_diagnostic({parsed.error().code, DiagnosticStage::Ingestion, parsed.error().location}).view());
        }
        const auto parsed = ingestor.ingest(bytes(inputs[3]));
        require(parsed.error().location.document_index == 0, "document position missing");
        require(parsed.error().location.transaction_index == 0, "transaction position missing");
        require(parsed.error().location.field == DiagnosticField::Date, "field identity missing");
        const auto absent = ingestor.ingest(bytes(inputs.back()));
        require(absent.error().code == DiagnosticCode::Required, "missing field reason lost");
        require(absent.error().location.field == DiagnosticField::AccountNumber, "required field identity lost");
    }
    // All four ABI entry points share the diagnostic boundary and atomic delivery.
    for (const auto& input : inputs) {
        for (auto call :
             {nordiska_pdf_v1_generate_customer_batch, nordiska_pdf_v1_generate_customer_batch_instrumented}) {
            require(call(bytes(input).data(), input.size(), receive_v1, nullptr) == 2,
                    "V1 invalid input status changed");
            check_private(nordiska_pdf_v1_get_last_error());
        }
        for (auto call :
             {nordiska_pdf_v2_generate_customer_batch, nordiska_pdf_v2_generate_customer_batch_instrumented}) {
            require(call(bytes(input).data(), input.size(), receive_v2, nullptr) == 2,
                    "V2 invalid input status changed");
            check_private(nordiska_pdf_v1_get_last_error());
        }
    }
    require(callbacks == 0, "failed generation delivered data");
    const auto input = valid.dump();
    for (auto call : {nordiska_pdf_v1_generate_customer_batch, nordiska_pdf_v1_generate_customer_batch_instrumented}) {
        require(call(bytes(input).data(), input.size(), receive_v1, nullptr) == 3, "callback failure status changed");
        check_private(nordiska_pdf_v1_get_last_error());
    }
    for (auto call : {nordiska_pdf_v2_generate_customer_batch, nordiska_pdf_v2_generate_customer_batch_instrumented}) {
        require(call(bytes(input).data(), input.size(), receive_v2, nullptr) == 3, "callback failure status changed");
        check_private(nordiska_pdf_v1_get_last_error());
    }
    // The ABI must swallow arbitrary callback exceptions without their text,
    // including non-std exceptions, and recover cleanly on the next call.
    const auto throwing_v1 =
        +[](const nordiska_pdf_batch_view*, void*) -> int { throw std::runtime_error(std::string(marker)); };
    const auto throwing_v2 =
        +[](const nordiska_pdf_batch_view_v2*, void*) -> int { throw std::runtime_error(std::string(marker)); };
    const auto unknown_v1 = +[](const nordiska_pdf_batch_view*, void*) -> int { throw marker; };
    const auto unknown_v2 = +[](const nordiska_pdf_batch_view_v2*, void*) -> int { throw marker; };
    const auto oom_v1 = +[](const nordiska_pdf_batch_view*, void*) -> int { throw std::bad_alloc{}; };
    const auto oom_v2 = +[](const nordiska_pdf_batch_view_v2*, void*) -> int { throw std::bad_alloc{}; };
    for (auto call : {nordiska_pdf_v1_generate_customer_batch, nordiska_pdf_v1_generate_customer_batch_instrumented}) {
        for (auto callback : {throwing_v1, unknown_v1}) {
            require(call(bytes(input).data(), input.size(), callback, nullptr) == 4,
                    "callback exception crossed V1 ABI");
            require(std::string_view(nordiska_pdf_v1_get_last_error()) == "boundary: unexpected failure",
                    "V1 exception diagnostic changed");
            check_private(nordiska_pdf_v1_get_last_error());
        }
        require(call(bytes(input).data(), input.size(), oom_v1, nullptr) == 6, "V1 callback bad_alloc status changed");
        check_private(nordiska_pdf_v1_get_last_error());
        require(call(
                    bytes(input).data(), input.size(), +[](const nordiska_pdf_batch_view*, void*) { return 0; },
                    nullptr) == 0,
                "V1 did not recover after callback exception");
        require(std::string_view(nordiska_pdf_v1_get_last_error()).empty(), "V1 retained a stale error");
    }
    for (auto call : {nordiska_pdf_v2_generate_customer_batch, nordiska_pdf_v2_generate_customer_batch_instrumented}) {
        for (auto callback : {throwing_v2, unknown_v2}) {
            require(call(bytes(input).data(), input.size(), callback, nullptr) == 4,
                    "callback exception crossed V2 ABI");
            require(std::string_view(nordiska_pdf_v1_get_last_error()) == "boundary: unexpected failure",
                    "V2 exception diagnostic changed");
            check_private(nordiska_pdf_v1_get_last_error());
        }
        require(call(bytes(input).data(), input.size(), oom_v2, nullptr) == 6, "V2 callback bad_alloc status changed");
        check_private(nordiska_pdf_v1_get_last_error());
        require(call(
                    bytes(input).data(), input.size(), +[](const nordiska_pdf_batch_view_v2*, void*) { return 0; },
                    nullptr) == 0,
                "V2 did not recover after callback exception");
        require(std::string_view(nordiska_pdf_v1_get_last_error()).empty(), "V2 retained a stale error");
    }
    const auto reentrant_v1 = +[](const nordiska_pdf_batch_view*, void*) {
        return nordiska_pdf_v2_generate_customer_batch(nullptr, 1, receive_v2, nullptr) == 1 ? 0 : 1;
    };
    const auto reentrant_v2 = +[](const nordiska_pdf_batch_view_v2*, void*) {
        return nordiska_pdf_v1_generate_customer_batch(nullptr, 1, receive_v1, nullptr) == 1 ? 0 : 1;
    };
    for (auto call : {nordiska_pdf_v1_generate_customer_batch, nordiska_pdf_v1_generate_customer_batch_instrumented}) {
        require(call(bytes(input).data(), input.size(), reentrant_v1, nullptr) == 0, "V1 reentrant call failed");
        require(std::string_view(nordiska_pdf_v1_get_last_error()).empty(), "V1 retained nested failure after success");
    }
    for (auto call : {nordiska_pdf_v2_generate_customer_batch, nordiska_pdf_v2_generate_customer_batch_instrumented}) {
        require(call(bytes(input).data(), input.size(), reentrant_v2, nullptr) == 0, "V2 reentrant call failed");
        require(std::string_view(nordiska_pdf_v1_get_last_error()).empty(), "V2 retained nested failure after success");
    }
    auto signer = std::make_shared<ThrowingSigner>();
    PdfGenerator generator({.engine = PdfEngineKind::Native, .enable_signing = true, .custom_signer = signer});
    auto result = generator.generate(bytes(input));
    require(!result, "unexpected signer exception must fail");
    require(result.error().diagnostic().code == DiagnosticCode::UnexpectedFailure, "exception was not translated");
    check_private(result.error().message());
    signer->out_of_memory = true;
    result = generator.generate(bytes(input));
    require(!result && result.error().diagnostic().code == DiagnosticCode::OutOfMemory, "bad_alloc was not translated");
    check_private(result.error().message());
    const auto extreme = format_diagnostic({DiagnosticCode::Required,
                                            DiagnosticStage::Ingestion,
                                            {.scope = DiagnosticScope::Transaction,
                                             .field = DiagnosticField::Date,
                                             .document_index = std::numeric_limits<size_t>::max(),
                                             .transaction_index = std::numeric_limits<size_t>::max()}});
    require(extreme.view().size() < 512, "diagnostic buffer exhausted");
    require(extreme.view().ends_with("date: required"), "diagnostic was truncated");
    for (int code = 0; code <= std::to_underlying(DiagnosticCode::SignerServiceReportedInternalError); ++code) {
        const Diagnostic diagnostic{static_cast<DiagnosticCode>(code),
                                    DiagnosticStage::Ingestion,
                                    {.scope = DiagnosticScope::Transaction,
                                     .field = DiagnosticField::AccountNumber,
                                     .document_index = std::numeric_limits<size_t>::max(),
                                     .transaction_index = std::numeric_limits<size_t>::max()}};
        const auto text = format_diagnostic(diagnostic);
        const auto reason = format_diagnostic({diagnostic.code});
        require(text.view().size() < 511, "formatter reason exceeds bounded capacity");
        require(text.view().ends_with(reason.view().substr(std::string_view("boundary: ").size())),
                "formatter truncated a reason");
    }
    const auto unknown = format_diagnostic({static_cast<DiagnosticCode>(9999),
                                            static_cast<DiagnosticStage>(9999),
                                            {.field = static_cast<DiagnosticField>(9999)}});
    require(unknown.view() == "unknown: unknown: unexpected failure", "unknown enums must use controlled fallback");
    std::cout << "Diagnostic privacy checks passed\n";
}
