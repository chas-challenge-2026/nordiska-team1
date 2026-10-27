#include "nordiska/layout/layout_builder.hpp"

#include "report_components.hpp"

#include <array>
#include <type_traits>
#include <utility>
#include <variant>

namespace nordiska {

std::expected<DocumentLayout, LayoutError> LayoutBuilder::build(const Document& doc, LayoutContext context) {
    return std::visit(
        [context](const auto& content) {
            if constexpr (std::is_same_v<std::decay_t<decltype(content)>, AccountStatement>) {
                return build_statement(content, context);
            } else {
                return build_tax_report(content, context);
            }
        },
        doc.content);
}

std::expected<DocumentLayout, LayoutError> LayoutBuilder::build_statement(const AccountStatement& stmt,
                                                                          LayoutContext context) {
    report::Composer page("Kontoutdrag", "Period: " + stmt.period, context.customer_name);
    if (!stmt.title.empty() && stmt.title != "Kontoutdrag") {
        page.section_heading(stmt.title);
        page.gap(5);
    }
    page.section_heading(stmt.account_name.empty() ? "Sparkonto" : stmt.account_name);
    page.field("Kontonummer: ", stmt.account_number);
    page.field("Valuta: ", stmt.currency);

    constexpr std::array columns = {
        report::Column{"Uppgift", 40},
        report::Column{"Datum", 17},
        report::Column{"Belopp", 21, true},
        report::Column{"Saldo", 22, true},
    };
    page.begin_table(columns);
    page.row(std::array<std::string, 4>{"Ingående saldo", "", "", stmt.opening_balance});
    for (const auto& tx : stmt.transactions) {
        std::string description = tx.type;
        if (!description.empty() && !tx.description.empty()) {
            description += " – ";
        }
        description += tx.description;
        page.row(std::array{std::move(description), tx.date, tx.amount_display, tx.balance_after_display});
    }
    if (stmt.transactions.empty()) {
        page.row(std::array<std::string, 4>{"Inga transaktioner under perioden", "", "", ""});
    }
    page.row(std::array<std::string, 4>{"Utgående saldo", "", "", stmt.closing_balance}, FontWeight::Bold);
    page.end_table();
    return page.finish();
}

std::expected<DocumentLayout, LayoutError> LayoutBuilder::build_tax_report(const AnnualTaxReport& tax,
                                                                           LayoutContext context) {
    // No annual reference is available: reuse the statement's page furniture
    // and table styling, with a content-specific summary and authority note.
    report::Composer page("Kontrolluppgift", "Inkomstår: " + tax.tax_year, context.customer_name);
    page.section_heading(tax.title.empty() ? "Kontrolluppgift för ränteinkomst" : tax.title);
    page.field("Kontonummer: ", tax.account_number);
    if (!tax.account_name.empty()) {
        page.field("Kontonamn: ", tax.account_name);
    }
    constexpr std::array columns = {report::Column{"Uppgift", 64}, report::Column{"Belopp", 36, true}};
    page.begin_table(columns);
    page.row(std::array{std::string("Total intjänad ränta"), tax.total_interest_earned});
    page.row(std::array{std::string("Preliminärskatt avdragen"), tax.preliminary_tax_deducted});
    page.end_table();
    page.field("Rapporterad till: ", tax.reported_to_authority);
    return page.finish();
}

} // namespace nordiska
