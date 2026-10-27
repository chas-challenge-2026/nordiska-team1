#pragma once

#include "nordiska/domain/document_types.hpp"
#include "nordiska/domain/pdf_rendering_job.hpp"
#include "nordiska/layout/document_layout.hpp"

#include <expected>
#include <string>
#include <string_view>

namespace nordiska {

enum class LayoutErrorKind {
    InvalidDocumentData,
    UnsupportedDocumentType,
    InternalError,
};

struct LayoutError {
    LayoutErrorKind kind{LayoutErrorKind::InternalError};
    std::string message;
};

struct LayoutContext {
    std::string_view customer_name;
};

class LayoutBuilder {
  public:
    [[nodiscard]] static std::expected<DocumentLayout, LayoutError> build(const Document& doc,
                                                                          LayoutContext context = {});
    [[nodiscard]] static std::expected<DocumentLayout, LayoutError> build_statement(const AccountStatement& stmt,
                                                                                    LayoutContext context = {});
    [[nodiscard]] static std::expected<DocumentLayout, LayoutError> build_tax_report(const AnnualTaxReport& tax,
                                                                                     LayoutContext context = {});
};

} // namespace nordiska
