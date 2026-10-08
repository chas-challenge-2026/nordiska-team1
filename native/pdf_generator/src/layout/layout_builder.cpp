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

void add_bank_header(PageLayout& page) {
    constexpr float kHeaderRightX = 400.0F;
    page.texts.push_back(PositionedText{
        .x = kHeaderRightX,
        .y = 54.0F,
        .text = std::string(kBankName),
        .font_size = 10.0F,
        .weight = FontWeight::Bold,
    });
    page.texts.push_back(PositionedText{
        .x = kHeaderRightX,
        .y = 68.0F,
        .text = std::string(kBankOrgNr),
        .font_size = 9.0F,
        .weight = FontWeight::Normal,
    });
    page.texts.push_back(PositionedText{
        .x = kHeaderRightX,
        .y = 82.0F,
        .text = std::string(kBankSeat),
        .font_size = 9.0F,
        .weight = FontWeight::Normal,
    });
    page.texts.push_back(PositionedText{
        .x = kHeaderRightX,
        .y = 96.0F,
        .text = std::string(kBankClearing),
        .font_size = 9.0F,
        .weight = FontWeight::Normal,
    });
}

void add_table_header(PageLayout& page, float y) {
    page.texts.push_back(
        PositionedText{.x = kMarginLeft, .y = y, .text = "Datum", .font_size = 10.0F, .weight = FontWeight::Bold});
    page.texts.push_back(
        PositionedText{.x = 130.0F, .y = y, .text = "Typ", .font_size = 10.0F, .weight = FontWeight::Bold});
    page.texts.push_back(
        PositionedText{.x = 210.0F, .y = y, .text = "Beskrivning", .font_size = 10.0F, .weight = FontWeight::Bold});
    page.texts.push_back(
        PositionedText{.x = 380.0F, .y = y, .text = "Belopp", .font_size = 10.0F, .weight = FontWeight::Bold});
    page.texts.push_back(
        PositionedText{.x = 470.0F, .y = y, .text = "Saldo", .font_size = 10.0F, .weight = FontWeight::Bold});

    page.lines.push_back(PositionedLine{
        .x1 = kMarginLeft,
        .y1 = y + 8.0F,
        .x2 = kMarginRight,
        .y2 = y + 8.0F,
        .line_width = 0.5F,
    });
}

void add_page_footers(DocumentLayout& doc) {
    const std::size_t total_pages = doc.pages.size();
    for (std::size_t i = 0; i < total_pages; ++i) {
        std::string page_text = "Sida " + std::to_string(i + 1) + " av " + std::to_string(total_pages);
        doc.pages[i].texts.push_back(PositionedText{
            .x = 490.0F,
            .y = 750.0F,
            .text = std::move(page_text),
            .font_size = 9.0F,
            .weight = FontWeight::Normal,
        });
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

    // 1. Title
    page.texts.push_back(PositionedText{
        .x = kMarginLeft,
        .y = 54.0F,
        .text = stmt.title.empty() ? "Kontoutdrag" : stmt.title,
        .font_size = 18.0F,
        .weight = FontWeight::Bold,
    });

    // 2. Bank Header (NOR-216)
    add_bank_header(page);

    // 3. Metadata
    std::string account_desc = "Konto: " + stmt.account_number;
    if (!stmt.account_name.empty()) {
        account_desc += " (" + stmt.account_name + ")";
    }
    page.texts.push_back(PositionedText{
        .x = kMarginLeft,
        .y = 80.0F,
        .text = std::move(account_desc),
        .font_size = 10.0F,
        .weight = FontWeight::Normal,
    });

    std::string period_currency = "Period: " + stmt.period;
    if (!stmt.currency.empty()) {
        period_currency += "   Valuta: " + stmt.currency;
    }
    page.texts.push_back(PositionedText{
        .x = kMarginLeft,
        .y = 96.0F,
        .text = std::move(period_currency),
        .font_size = 10.0F,
        .weight = FontWeight::Normal,
    });

    std::string balances = "Ingående saldo: " + stmt.opening_balance + "   Utgående saldo: " + stmt.closing_balance;
    page.texts.push_back(PositionedText{
        .x = kMarginLeft,
        .y = 112.0F,
        .text = std::move(balances),
        .font_size = 10.0F,
        .weight = FontWeight::Normal,
    });

    // 4. Divider
    page.lines.push_back(PositionedLine{
        .x1 = kMarginLeft,
        .y1 = 124.0F,
        .x2 = kMarginRight,
        .y2 = 124.0F,
        .line_width = 1.0F,
    });

    // 5. Table Header
    add_table_header(page, 138.0F);

    // 6. Transaction Rows
    float current_y = 162.0F;

    if (stmt.transactions.empty()) {
        page.texts.push_back(PositionedText{
            .x = kMarginLeft,
            .y = current_y,
            .text = "(Inga transaktioner under perioden)",
            .font_size = 10.0F,
            .weight = FontWeight::Normal,
        });
    } else {
        for (const auto& tx : stmt.transactions) {
            if (current_y > kMaxY) {
                doc.pages.push_back(std::move(page));
                page = create_page();
                // NOR-215: Re-render table headers at y = 54.0F on subsequent pages
                add_table_header(page, 54.0F);
                current_y = 78.0F;
            }

            page.texts.push_back(PositionedText{
                .x = kMarginLeft, .y = current_y, .text = tx.date, .font_size = 9.0F, .weight = FontWeight::Normal});
            page.texts.push_back(PositionedText{
                .x = 130.0F, .y = current_y, .text = tx.type, .font_size = 9.0F, .weight = FontWeight::Normal});
            page.texts.push_back(PositionedText{
                .x = 210.0F, .y = current_y, .text = tx.description, .font_size = 9.0F, .weight = FontWeight::Normal});
            page.texts.push_back(PositionedText{.x = 380.0F,
                                                .y = current_y,
                                                .text = tx.amount_display,
                                                .font_size = 9.0F,
                                                .weight = FontWeight::Normal});
            page.texts.push_back(PositionedText{.x = 470.0F,
                                                .y = current_y,
                                                .text = tx.balance_after_display,
                                                .font_size = 9.0F,
                                                .weight = FontWeight::Normal});

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

    // 1. Title
    page.texts.push_back(PositionedText{
        .x = kMarginLeft,
        .y = 54.0F,
        .text = tax.title.empty() ? "Kontrolluppgift för ränteinkomst" : tax.title,
        .font_size = 18.0F,
        .weight = FontWeight::Bold,
    });

    // 2. Bank Header (NOR-216)
    add_bank_header(page);

    // 3. Tax Year
    page.texts.push_back(PositionedText{
        .x = kMarginLeft,
        .y = 80.0F,
        .text = "Inkomstår: " + tax.tax_year,
        .font_size = 13.0F,
        .weight = FontWeight::Bold,
    });

    // 4. Divider
    page.lines.push_back(PositionedLine{
        .x1 = kMarginLeft,
        .y1 = 110.0F,
        .x2 = kMarginRight,
        .y2 = 110.0F,
        .line_width = 1.0F,
    });

    // 5. Details
    page.texts.push_back(PositionedText{
        .x = kMarginLeft,
        .y = 130.0F,
        .text = "Kontonummer:              " + tax.account_number,
        .font_size = 11.0F,
        .weight = FontWeight::Normal,
    });

    if (!tax.account_name.empty()) {
        page.texts.push_back(PositionedText{
            .x = kMarginLeft,
            .y = 150.0F,
            .text = "Kontonamn:                " + tax.account_name,
            .font_size = 11.0F,
            .weight = FontWeight::Normal,
        });
    }

    page.texts.push_back(PositionedText{
        .x = kMarginLeft,
        .y = 170.0F,
        .text = "Total intjänad ränta:     " + tax.total_interest_earned,
        .font_size = 11.0F,
        .weight = FontWeight::Normal,
    });

    page.texts.push_back(PositionedText{
        .x = kMarginLeft,
        .y = 190.0F,
        .text = "Preliminärskatt avdragen: " + tax.preliminary_tax_deducted,
        .font_size = 11.0F,
        .weight = FontWeight::Normal,
    });

    page.texts.push_back(PositionedText{
        .x = kMarginLeft,
        .y = 210.0F,
        .text = "Rapporterad till:         " + tax.reported_to_authority,
        .font_size = 11.0F,
        .weight = FontWeight::Normal,
    });

    // 6. Divider
    page.lines.push_back(PositionedLine{
        .x1 = kMarginLeft,
        .y1 = 230.0F,
        .x2 = kMarginRight,
        .y2 = 230.0F,
        .line_width = 0.5F,
    });

    // 7. Authority Note
    page.texts.push_back(PositionedText{
        .x = kMarginLeft,
        .y = 250.0F,
        .text = "Fastställd kontrolluppgift enligt Skatteverkets föreskrifter.",
        .font_size = 9.0F,
        .weight = FontWeight::Normal,
    });

    doc.pages.push_back(std::move(page));
    // NOR-215: Add page number footer ("Sida X av Y") at the bottom of each page
    add_page_footers(doc);
    return doc;
}

} // namespace nordiska
