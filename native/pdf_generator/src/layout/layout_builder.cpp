#include "nordiska/layout/layout_builder.hpp"

#include <variant>

namespace nordiska {

namespace {

constexpr float kPageWidth = 612.0F;
constexpr float kPageHeight = 792.0F;
constexpr float kMarginLeft = 54.0F;
constexpr float kMarginRight = 558.0F;
constexpr float kMaxY = 720.0F;

PageLayout create_page() {
    return PageLayout{
        .width = kPageWidth,
        .height = kPageHeight,
        .texts = {},
        .lines = {},
    };
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

    // 2. Metadata
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

    // 3. Divider
    page.lines.push_back(PositionedLine{
        .x1 = kMarginLeft,
        .y1 = 124.0F,
        .x2 = kMarginRight,
        .y2 = 124.0F,
        .line_width = 1.0F,
    });

    // 4. Table Header
    page.texts.push_back(
        PositionedText{.x = kMarginLeft, .y = 138.0F, .text = "Datum", .font_size = 10.0F, .weight = FontWeight::Bold});
    page.texts.push_back(
        PositionedText{.x = 130.0F, .y = 138.0F, .text = "Typ", .font_size = 10.0F, .weight = FontWeight::Bold});
    page.texts.push_back(PositionedText{
        .x = 210.0F, .y = 138.0F, .text = "Beskrivning", .font_size = 10.0F, .weight = FontWeight::Bold});
    page.texts.push_back(
        PositionedText{.x = 380.0F, .y = 138.0F, .text = "Belopp", .font_size = 10.0F, .weight = FontWeight::Bold});
    page.texts.push_back(
        PositionedText{.x = 470.0F, .y = 138.0F, .text = "Saldo", .font_size = 10.0F, .weight = FontWeight::Bold});

    page.lines.push_back(PositionedLine{
        .x1 = kMarginLeft,
        .y1 = 146.0F,
        .x2 = kMarginRight,
        .y2 = 146.0F,
        .line_width = 0.5F,
    });

    // 5. Transaction Rows
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
                current_y = 54.0F;
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

    // 2. Tax Year
    page.texts.push_back(PositionedText{
        .x = kMarginLeft,
        .y = 80.0F,
        .text = "Inkomstår: " + tax.tax_year,
        .font_size = 13.0F,
        .weight = FontWeight::Bold,
    });

    // 3. Divider
    page.lines.push_back(PositionedLine{
        .x1 = kMarginLeft,
        .y1 = 94.0F,
        .x2 = kMarginRight,
        .y2 = 94.0F,
        .line_width = 1.0F,
    });

    // 4. Details
    page.texts.push_back(PositionedText{
        .x = kMarginLeft,
        .y = 120.0F,
        .text = "Kontonummer:              " + tax.account_number,
        .font_size = 11.0F,
        .weight = FontWeight::Normal,
    });

    if (!tax.account_name.empty()) {
        page.texts.push_back(PositionedText{
            .x = kMarginLeft,
            .y = 140.0F,
            .text = "Kontonamn:                " + tax.account_name,
            .font_size = 11.0F,
            .weight = FontWeight::Normal,
        });
    }

    page.texts.push_back(PositionedText{
        .x = kMarginLeft,
        .y = 160.0F,
        .text = "Total intjänad ränta:     " + tax.total_interest_earned,
        .font_size = 11.0F,
        .weight = FontWeight::Normal,
    });

    page.texts.push_back(PositionedText{
        .x = kMarginLeft,
        .y = 180.0F,
        .text = "Preliminärskatt avdragen: " + tax.preliminary_tax_deducted,
        .font_size = 11.0F,
        .weight = FontWeight::Normal,
    });

    page.texts.push_back(PositionedText{
        .x = kMarginLeft,
        .y = 200.0F,
        .text = "Rapporterad till:         " + tax.reported_to_authority,
        .font_size = 11.0F,
        .weight = FontWeight::Normal,
    });

    // 5. Divider
    page.lines.push_back(PositionedLine{
        .x1 = kMarginLeft,
        .y1 = 220.0F,
        .x2 = kMarginRight,
        .y2 = 220.0F,
        .line_width = 0.5F,
    });

    // 6. Authority Note
    page.texts.push_back(PositionedText{
        .x = kMarginLeft,
        .y = 240.0F,
        .text = "Fastställd kontrolluppgift enligt Skatteverkets föreskrifter.",
        .font_size = 9.0F,
        .weight = FontWeight::Normal,
    });

    doc.pages.push_back(std::move(page));
    return doc;
}

} // namespace nordiska
