#include "nordiska/ingestion/json_ingestor.hpp"

#include "nordiska/domain/document_types.hpp"
#include "nordiska/domain/pdf_rendering_job.hpp"

#include <cstddef>
#include <cstdint>
#include <expected>
#include <memory>
#include <nlohmann/json.hpp>
#include <span>
#include <string>
#include <string_view>
#include <unordered_set>
#include <variant>
#include <vector>

namespace nordiska {

struct JsonIngestor::Impl {
    virtual ~Impl() = default;
    [[nodiscard]] virtual std::expected<PdfRenderingJob, IngestError>
    ingest(std::span<const uint8_t> json_utf8) const = 0;
};

namespace {

using json = nlohmann::json;

std::expected<std::string, IngestError> get_required_string(const json& obj, std::string_view key,
                                                            std::string_view breadcrumb) {
    if (!obj.contains(key)) {
        return std::unexpected(IngestError{
            .kind = IngestErrorKind::InvalidInput,
            .path = std::string(breadcrumb),
            .message = "missing required field '" + std::string(key) + "'",
        });
    }
    const auto& val = obj.at(std::string(key));
    if (!val.is_string()) {
        return std::unexpected(IngestError{
            .kind = IngestErrorKind::InvalidInput,
            .path = std::string(breadcrumb) + "." + std::string(key),
            .message = "field '" + std::string(key) + "' must be a string",
        });
    }
    return val.get<std::string>();
}

std::string get_optional_string(const json& obj, std::string_view key, std::string_view fallback = "") {
    if (!obj.contains(key)) {
        return std::string(fallback);
    }
    const auto& val = obj.at(std::string(key));
    if (val.is_string()) {
        return val.get<std::string>();
    }
    return std::string(fallback);
}

std::expected<StatementTransaction, IngestError> parse_transaction(const json& tx_obj, std::string_view breadcrumb) {
    if (!tx_obj.is_object()) {
        return std::unexpected(IngestError{
            .kind = IngestErrorKind::InvalidInput,
            .path = std::string(breadcrumb),
            .message = "transaction must be a JSON object",
        });
    }

    StatementTransaction tx;
    auto date_res = get_required_string(tx_obj, "date", breadcrumb);
    if (!date_res) {
        return std::unexpected(date_res.error());
    }
    tx.date = std::move(*date_res);

    tx.type = get_optional_string(tx_obj, "type");
    tx.description = get_optional_string(tx_obj, "description");
    tx.currency = get_optional_string(tx_obj, "currency", "SEK");

    if (tx_obj.contains("amount_minor")) {
        if (!tx_obj.at("amount_minor").is_number_integer()) {
            return std::unexpected(IngestError{
                .kind = IngestErrorKind::InvalidInput,
                .path = std::string(breadcrumb) + ".amount_minor",
                .message = "amount_minor must be an integer",
            });
        }
        tx.amount_minor = tx_obj.at("amount_minor").get<std::int64_t>();
    }

    tx.amount_display = get_optional_string(tx_obj, "amount_display");
    tx.balance_after_display = get_optional_string(tx_obj, "balance_after_display");

    return tx;
}

std::expected<AccountStatement, IngestError> parse_account_statement(const json& doc_obj, std::string_view breadcrumb) {
    if (!doc_obj.is_object()) {
        return std::unexpected(IngestError{
            .kind = IngestErrorKind::InvalidInput,
            .path = std::string(breadcrumb),
            .message = "document must be a JSON object",
        });
    }

    AccountStatement statement;
    statement.title = get_optional_string(doc_obj, "title", "Kontoutdrag");

    auto acct_num_res = get_required_string(doc_obj, "account_number", breadcrumb);
    if (!acct_num_res) {
        return std::unexpected(acct_num_res.error());
    }
    statement.account_number = std::move(*acct_num_res);

    statement.account_name = get_optional_string(doc_obj, "account_name");
    statement.currency = get_optional_string(doc_obj, "currency", "SEK");
    statement.period = get_optional_string(doc_obj, "period");
    statement.opening_balance = get_optional_string(doc_obj, "opening_balance");
    statement.closing_balance = get_optional_string(doc_obj, "closing_balance");

    if (doc_obj.contains("transactions")) {
        const auto& tx_list = doc_obj.at("transactions");
        if (!tx_list.is_array()) {
            return std::unexpected(IngestError{
                .kind = IngestErrorKind::InvalidInput,
                .path = std::string(breadcrumb) + ".transactions",
                .message = "transactions must be an array",
            });
        }

        statement.transactions.reserve(tx_list.size());
        for (size_t i = 0; i < tx_list.size(); ++i) {
            const std::string tx_breadcrumb = std::string(breadcrumb) + ".transactions[" + std::to_string(i) + "]";
            auto parsed_tx = parse_transaction(tx_list.at(i), tx_breadcrumb);
            if (!parsed_tx) {
                return std::unexpected(parsed_tx.error());
            }
            statement.transactions.emplace_back(std::move(*parsed_tx));
        }
    }

    return statement;
}

std::expected<AnnualTaxReport, IngestError> parse_annual_tax_report(const json& doc_obj, std::string_view breadcrumb) {
    if (!doc_obj.is_object()) {
        return std::unexpected(IngestError{
            .kind = IngestErrorKind::InvalidInput,
            .path = std::string(breadcrumb),
            .message = "document must be a JSON object",
        });
    }

    AnnualTaxReport report;
    report.title = get_optional_string(doc_obj, "title", "Kontrolluppgift för ränteinkomst");

    auto tax_year_res = get_required_string(doc_obj, "tax_year", breadcrumb);
    if (!tax_year_res) {
        return std::unexpected(tax_year_res.error());
    }
    report.tax_year = std::move(*tax_year_res);

    auto acct_num_res = get_required_string(doc_obj, "account_number", breadcrumb);
    if (!acct_num_res) {
        return std::unexpected(acct_num_res.error());
    }
    report.account_number = std::move(*acct_num_res);

    report.account_name = get_optional_string(doc_obj, "account_name");
    report.total_interest_earned = get_optional_string(doc_obj, "total_interest_earned");
    report.preliminary_tax_deducted = get_optional_string(doc_obj, "preliminary_tax_deducted");
    report.reported_to_authority = get_optional_string(doc_obj, "reported_to_authority");

    return report;
}

class NlohmannIngestorImpl final : public JsonIngestor::Impl {
  public:
    [[nodiscard]] std::expected<PdfRenderingJob, IngestError>
    ingest(std::span<const uint8_t> json_utf8) const override {
        json root;
        try {
            root = json::parse(json_utf8.begin(), json_utf8.end());
        } catch (const json::parse_error& err) {
            return std::unexpected(IngestError{
                .kind = IngestErrorKind::InvalidInput,
                .path = "root",
                .message = "Malformed JSON payload: " + std::string(err.what()),
            });
        }

        if (!root.is_object()) {
            return std::unexpected(IngestError{
                .kind = IngestErrorKind::InvalidInput,
                .path = "root",
                .message = "Root element must be a JSON object",
            });
        }

        PdfRenderingJob job;

        // Validate customer_id
        if (!root.contains("customer_id")) {
            return std::unexpected(IngestError{
                .kind = IngestErrorKind::InvalidInput,
                .path = "customer_id",
                .message = "missing required field 'customer_id'",
            });
        }
        const auto& cust_id_val = root.at("customer_id");
        if (!cust_id_val.is_number_unsigned() && !cust_id_val.is_number_integer()) {
            return std::unexpected(IngestError{
                .kind = IngestErrorKind::InvalidInput,
                .path = "customer_id",
                .message = "customer_id must be an unsigned integer",
            });
        }
        const auto raw_cust_id = cust_id_val.get<std::int64_t>();
        if (raw_cust_id <= 0) {
            return std::unexpected(IngestError{
                .kind = IngestErrorKind::InvalidInput,
                .path = "customer_id",
                .message = "customer_id must be greater than zero",
            });
        }
        job.customer_id = static_cast<std::uint64_t>(raw_cust_id);

        job.schema_version = get_optional_string(root, "$schema_version", "1.0");
        job.customer_name = get_optional_string(root, "customer_name");
        job.created_at = get_optional_string(root, "created_at");

        // Validate documents array
        if (!root.contains("documents")) {
            return std::unexpected(IngestError{
                .kind = IngestErrorKind::InvalidInput,
                .path = "documents",
                .message = "missing required field 'documents'",
            });
        }
        const auto& docs_json = root.at("documents");
        if (!docs_json.is_array()) {
            return std::unexpected(IngestError{
                .kind = IngestErrorKind::InvalidInput,
                .path = "documents",
                .message = "field 'documents' must be an array",
            });
        }
        if (docs_json.empty()) {
            return std::unexpected(IngestError{
                .kind = IngestErrorKind::InvalidInput,
                .path = "documents",
                .message = "documents array must not be empty",
            });
        }

        std::unordered_set<std::string> seen_document_ids;
        job.documents.reserve(docs_json.size());

        for (size_t i = 0; i < docs_json.size(); ++i) {
            const std::string doc_breadcrumb = "documents[" + std::to_string(i) + "]";
            const auto& doc_entry = docs_json.at(i);
            if (!doc_entry.is_object()) {
                return std::unexpected(IngestError{
                    .kind = IngestErrorKind::InvalidInput,
                    .path = doc_breadcrumb,
                    .message = "document entry must be a JSON object",
                });
            }

            // document_id
            auto id_res = get_required_string(doc_entry, "document_id", doc_breadcrumb);
            if (!id_res) {
                return std::unexpected(id_res.error());
            }
            const std::string& doc_id = *id_res;
            if (doc_id.empty()) {
                return std::unexpected(IngestError{
                    .kind = IngestErrorKind::InvalidInput,
                    .path = doc_breadcrumb + ".document_id",
                    .message = "document_id must not be empty",
                });
            }
            if (seen_document_ids.contains(doc_id)) {
                return std::unexpected(IngestError{
                    .kind = IngestErrorKind::InvalidInput,
                    .path = doc_breadcrumb + ".document_id",
                    .message = "Duplicate document_id: " + doc_id,
                });
            }
            seen_document_ids.insert(doc_id);

            // kind
            auto kind_res = get_required_string(doc_entry, "kind", doc_breadcrumb);
            if (!kind_res) {
                return std::unexpected(kind_res.error());
            }
            const std::string& kind_str = *kind_res;

            // document payload object
            if (!doc_entry.contains("document")) {
                return std::unexpected(IngestError{
                    .kind = IngestErrorKind::InvalidInput,
                    .path = doc_breadcrumb,
                    .message = "missing required field 'document'",
                });
            }
            const auto& doc_payload = doc_entry.at("document");

            Document doc;
            doc.document_id = doc_id;
            doc.version = get_optional_string(doc_entry, "version", "1.0");

            if (kind_str == "account_statement") {
                doc.kind = DocumentKind::AccountStatement;
                auto parsed_statement = parse_account_statement(doc_payload, doc_breadcrumb + ".document");
                if (!parsed_statement) {
                    return std::unexpected(parsed_statement.error());
                }
                doc.content = std::move(*parsed_statement);
            } else if (kind_str == "annual_tax_report") {
                doc.kind = DocumentKind::AnnualTaxReport;
                auto parsed_tax = parse_annual_tax_report(doc_payload, doc_breadcrumb + ".document");
                if (!parsed_tax) {
                    return std::unexpected(parsed_tax.error());
                }
                doc.content = std::move(*parsed_tax);
            } else {
                return std::unexpected(IngestError{
                    .kind = IngestErrorKind::InvalidInput,
                    .path = doc_breadcrumb + ".kind",
                    .message = "unsupported document kind: " + kind_str,
                });
            }

            job.documents.emplace_back(std::move(doc));
        }

        return job;
    }
};

class SimdjsonIngestorImpl final : public JsonIngestor::Impl {
  public:
    [[nodiscard]] std::expected<PdfRenderingJob, IngestError> ingest(std::span<const uint8_t>) const override {
        return std::unexpected(IngestError{
            .kind = IngestErrorKind::InternalError,
            .path = "ingestor",
            .message = "simdjson ingestor engine is not yet implemented",
        });
    }
};

} // namespace

JsonIngestor::JsonIngestor(JsonIngestorKind kind) {
    switch (kind) {
    case JsonIngestorKind::Nlohmann:
        impl_ = std::make_unique<NlohmannIngestorImpl>();
        break;
    case JsonIngestorKind::Simdjson:
        impl_ = std::make_unique<SimdjsonIngestorImpl>();
        break;
    }
}

JsonIngestor::~JsonIngestor() = default;

JsonIngestor::JsonIngestor(JsonIngestor&&) noexcept = default;
JsonIngestor& JsonIngestor::operator=(JsonIngestor&&) noexcept = default;

std::expected<PdfRenderingJob, IngestError> JsonIngestor::ingest(std::span<const uint8_t> json_utf8) const {
    return impl_->ingest(json_utf8);
}

} // namespace nordiska
