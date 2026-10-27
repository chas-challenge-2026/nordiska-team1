#include "nordiska/layout/layout_builder.hpp"

#include <variant>

namespace nordiska {

namespace {

constexpr float kPageWidth = 612.0F;
constexpr float kPageHeight = 792.0F;
constexpr float kMarginLeft = 54.0F;
constexpr float kMarginRight = 558.0F;
constexpr float kMaxY = 720.0F;

// Static bank metadata (NOR-216)
constexpr std::string_view kBankName = "Nordiska Sparbanken AB";
constexpr std::string_view kBankOrgNr = "Org.nr 556123-4567";
constexpr std::string_view kBankSeat = "Säte: Stockholm";
constexpr std::string_view kBankClearing = "Clearing: 9020";

PageLayout create_page() {
    return PageLayout{
        .width = kPageWidth,
        .height = kPageHeight,
        .texts = {},
        .lines = {},
    };
}

void add_text(PageLayout& page, float x, float y, std::string text, float font_size,
              FontWeight weight = FontWeight::Normal) {
    page.texts.push_back(PositionedText{
        .x = x,
        .y = y,
        .text = std::move(text),
        .font_size = font_size,
        .weight = weight,
    });
}

void add_horizontal_line(PageLayout& page, float y, float line_width) {
    page.lines.push_back(PositionedLine{
        .x1 = kMarginLeft,
        .y1 = y,
        .x2 = kMarginRight,
        .y2 = y,
        .line_width = line_width,
    });
}

void add_bank_header(PageLayout& page) {
    constexpr float kHeaderRightX = 400.0F;
    add_text(page, kHeaderRightX, 54.0F, std::string(kBankName), 10.0F, FontWeight::Bold);
    add_text(page, kHeaderRightX, 68.0F, std::string(kBankOrgNr), 9.0F);
    add_text(page, kHeaderRightX, 82.0F, std::string(kBankSeat), 9.0F);
    add_text(page, kHeaderRightX, 96.0F, std::string(kBankClearing), 9.0F);
}

void add_table_header(PageLayout& page, float y) {
    add_text(page, kMarginLeft, y, "Datum", 10.0F, FontWeight::Bold);
    add_text(page, 130.0F, y, "Typ", 10.0F, FontWeight::Bold);
    add_text(page, 210.0F, y, "Beskrivning", 10.0F, FontWeight::Bold);
    add_text(page, 380.0F, y, "Belopp", 10.0F, FontWeight::Bold);
    add_text(page, 470.0F, y, "Saldo", 10.0F, FontWeight::Bold);
    add_horizontal_line(page, y + 8.0F, 0.5F);
}

void add_page_footers(DocumentLayout& doc) {
    const std::size_t total_pages = doc.pages.size();
    for (std::size_t i = 0; i < total_pages; ++i) {
        std::string page_text = "Sida " + std::to_string(i + 1) + " av " + std::to_string(total_pages);
        add_text(doc.pages[i], 490.0F, 750.0F, std::move(page_text), 9.0F);
    }
}

} // namespace

std::expected<DocumentLayout, LayoutError> LayoutBuilder::build(const Document& doc) {
    if (std::holds_alternative<AccountStatement>(doc.content)) {
        return build_statement(std::get<AccountStatement>(doc.content));
    }
    if (std::holds_alternative<AnnualTaxReport>(doc.content)) {
        return build_tax_report(std::get<AnnualTaxReport>(doc.content));
    }
    return std::unexpected(LayoutError{
        .kind = LayoutErrorKind::UnsupportedDocumentType,
        .message = "Unsupported document content type",
    });
}

std::expected<DocumentLayout, LayoutError> LayoutBuilder::build_statement(const AccountStatement& stmt) {
    DocumentLayout doc;
    PageLayout page = create_page();

    // 1. Title & Bank Header (NOR-216)
    add_text(page, kMarginLeft, 54.0F, stmt.title.empty() ? "Kontoutdrag" : stmt.title, 18.0F, FontWeight::Bold);
    add_bank_header(page);

    // 2. Metadata
    std::string account_desc = "Konto: " + stmt.account_number;
    if (!stmt.account_name.empty()) {
        account_desc += " (" + stmt.account_name + ")";
    }
    add_text(page, kMarginLeft, 80.0F, std::move(account_desc), 10.0F);

    std::string period_currency = "Period: " + stmt.period;
    if (!stmt.currency.empty()) {
        period_currency += "   Valuta: " + stmt.currency;
    }
    add_text(page, kMarginLeft, 96.0F, std::move(period_currency), 10.0F);

    std::string balances = "Ingående saldo: " + stmt.opening_balance + "   Utgående saldo: " + stmt.closing_balance;
    add_text(page, kMarginLeft, 112.0F, std::move(balances), 10.0F);

    // 3. Divider & Table Header
    add_horizontal_line(page, 124.0F, 1.0F);
    add_table_header(page, 138.0F);

    // 4. Transaction Rows
    float current_y = 162.0F;

    if (stmt.transactions.empty()) {
        add_text(page, kMarginLeft, current_y, "(Inga transaktioner under perioden)", 10.0F);
    } else {
        for (const auto& tx : stmt.transactions) {
            if (current_y > kMaxY) {
                doc.pages.push_back(std::move(page));
                page = create_page();
                // NOR-215: Re-render table headers at y = 54.0F on subsequent pages
                add_table_header(page, 54.0F);
                current_y = 78.0F;
            }

            add_text(page, kMarginLeft, current_y, tx.date, 9.0F);
            add_text(page, 130.0F, current_y, tx.type, 9.0F);
            add_text(page, 210.0F, current_y, tx.description, 9.0F);
            add_text(page, 380.0F, current_y, tx.amount_display, 9.0F);
            add_text(page, 470.0F, current_y, tx.balance_after_display, 9.0F);

            current_y += 18.0F;
        }
    }

    doc.pages.push_back(std::move(page));
    // NOR-215: Add page number footer ("Sida X av Y") at the bottom of each page
    add_page_footers(doc);
    return doc;
}

std::expected<DocumentLayout, LayoutError> LayoutBuilder::build_tax_report(const AnnualTaxReport& tax) {
    DocumentLayout doc;
    PageLayout page = create_page();

    // 1. Title & Bank Header (NOR-216)
    add_text(page, kMarginLeft, 54.0F, tax.title.empty() ? "Kontrolluppgift för ränteinkomst" : tax.title, 18.0F,
             FontWeight::Bold);
    add_bank_header(page);

    // 2. Tax Year & Divider
    add_text(page, kMarginLeft, 80.0F, "Inkomstår: " + tax.tax_year, 13.0F, FontWeight::Bold);
    add_horizontal_line(page, 110.0F, 1.0F);

    // 3. Details
    add_text(page, kMarginLeft, 130.0F, "Kontonummer:              " + tax.account_number, 11.0F);
    if (!tax.account_name.empty()) {
        add_text(page, kMarginLeft, 150.0F, "Kontonamn:                " + tax.account_name, 11.0F);
    }
    add_text(page, kMarginLeft, 170.0F, "Total intjänad ränta:     " + tax.total_interest_earned, 11.0F);
    add_text(page, kMarginLeft, 190.0F, "Preliminärskatt avdragen: " + tax.preliminary_tax_deducted, 11.0F);
    add_text(page, kMarginLeft, 210.0F, "Rapporterad till:         " + tax.reported_to_authority, 11.0F);

    // 4. Divider & Authority Note
    add_horizontal_line(page, 230.0F, 0.5F);
    add_text(page, kMarginLeft, 250.0F, "Fastställd kontrolluppgift enligt Skatteverkets föreskrifter.", 9.0F);

    doc.pages.push_back(std::move(page));
    // NOR-215: Add page number footer ("Sida X av Y") at the bottom of each page
    add_page_footers(doc);
    return doc;
}

} // namespace nordiska
