#pragma once

#include "nordiska/domain/document_types.hpp"

#include <cstdint>
#include <string>
#include <variant>
#include <vector>

namespace nordiska {

enum class DocumentKind {
    AccountStatement,
    AnnualTaxReport,
};

using DocumentContent = std::variant<AccountStatement, AnnualTaxReport>;

struct Document {
    std::string document_id;
    DocumentKind kind{DocumentKind::AccountStatement};
    std::string version;
    DocumentContent content;
};

struct PdfRenderingJob {
    std::string schema_version;
    std::uint64_t customer_id{0};
    std::string customer_name;
    std::string created_at;
    std::vector<Document> documents;
};

} // namespace nordiska
