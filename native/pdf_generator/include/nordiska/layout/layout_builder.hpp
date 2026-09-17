#pragma once

#include "nordiska/domain/document_types.hpp"
#include "nordiska/domain/pdf_rendering_job.hpp"
#include "nordiska/layout/document_layout.hpp"

namespace nordiska {

class LayoutBuilder {
  public:
    [[nodiscard]] static DocumentLayout build(const Document& doc);
    [[nodiscard]] static DocumentLayout build_statement(const AccountStatement& stmt);
    [[nodiscard]] static DocumentLayout build_tax_report(const AnnualTaxReport& tax);
};

} // namespace nordiska
