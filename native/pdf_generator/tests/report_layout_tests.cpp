#include "nordiska/layout/layout_builder.hpp"
#include "nordiska/layout/report_style.hpp"
#include "nordiska/rendering/pdf_engine.hpp"
#include "nordiska/text/text_metrics.hpp"
#include "pdf_test_helpers.hpp"

#include <algorithm>
#include <array>
#include <cmath>
#include <iostream>
#include <stdexcept>
#include <string>

namespace {
using namespace nordiska;
using namespace std::string_view_literals;

void require(bool condition, const char* message) {
    if (!condition) {
        throw std::runtime_error(message);
    }
}

bool has_text(const PageLayout& page, std::string_view value) {
    return std::ranges::any_of(page.commands, [&](const auto& command) {
        const auto* text = std::get_if<PositionedText>(&command);
        return text && text->text == value;
    });
}

void check_bounds(const DocumentLayout& layout) {
    for (const auto& page : layout.pages) {
        require(page.width == kNordiskaReportStyle.page_width && page.height == kNordiskaReportStyle.page_height,
                "Reports must use A4 geometry");
        for (const auto& command : page.commands) {
            if (const auto* text = std::get_if<PositionedText>(&command)) {
                require(text->x >= 0 && text->x + text_width(text->text, text->font_size, text->weight) <= page.width,
                        "Text must remain within page width");
                require(text->y >= text->font_size && text->y < page.height, "Text must remain within page height");
                require(text->y < 770 || text->font_size <= 7, "Body content must not overlap the footer");
            }
        }
    }
}

void test_wrapping() {
    require(std::abs(text_width("100,00 SEK", 10) - 53.37F) < 0.02F, "Helvetica metric regression");
    const auto lines = wrap_text("ÅÄÖ åäö – ränta 100,00 €", 50, 10);
    require(lines.size() > 1, "Swedish text must wrap");
    for (const auto& line : lines) {
        require(text_width(line, 10) <= 50.01F, "Wrapped text must fit measured width");
        require(line.find('?') == std::string::npos, "Swedish and WinAnsi typography must be preserved");
    }
    const std::string huge(10000, 'W');
    std::string joined;
    for (const auto& line : wrap_text(huge, 80, 10)) {
        require(text_width(line, 10) <= 80.01F, "Unbroken tokens must fit");
        joined += line;
    }
    require(joined == huge, "Wrapping must not truncate unbroken input");
    require(wrap_text("first\nsecond", 200, 10) == std::vector<std::string>{"first", "second"},
            "Explicit line breaks must be preserved");
    require(display_text("A\tB\0C"sv) == "A B C", "Controls must not truncate rendered text");
}

AccountStatement statement() {
    return AccountStatement{
        .account_number = "DEMO-10001",
        .account_name = "Nordiska Flex",
        .currency = "SEK",
        .period = "2026-01-01 – 2026-01-31",
        .opening_balance = "90 000,00 SEK",
        .closing_balance = "125 000,00 SEK",
    };
}

void test_branding_and_continuations() {
    auto stmt = statement();
    for (int i = 0; i < 100; ++i) {
        stmt.transactions.push_back(StatementTransaction{
            .date = "2026-01-15",
            .type = "Överföring",
            .description = "Transaktion " + std::to_string(i),
            .amount_display = "−1 234 567,89 SEK",
            .balance_after_display = "9 876 543,21 SEK",
        });
    }
    const auto layout = LayoutBuilder::build_statement(stmt, {.customer_name = "Anna Exempel"});
    require(layout.has_value() && layout->pages.size() > 2, "Transactions must paginate");
    check_bounds(*layout);
    require(has_text(layout->pages.front(), "Anna Exempel"), "Customer name must reach the layout");
    size_t dates = 0;
    for (size_t i = 0; i < layout->pages.size(); ++i) {
        const auto& page = layout->pages[i];
        require(std::holds_alternative<PositionedImage>(page.commands.front()), "Each page must include the logo");
        require(std::get<PositionedImage>(page.commands.front()).image == &nordiska_logo(),
                "Pages must borrow one immutable prepared logo");
        require(has_text(page, "Kontoutdrag"), "Every page must retain the document heading");
        require(has_text(page, "Uppgift") && has_text(page, "Datum") && has_text(page, "Belopp") &&
                    has_text(page, "Saldo"),
                "Every table continuation must repeat the column headers");
        require(has_text(page, "Nordiska Bankaktiebolaget | Organisationsnummer: 556760-6032"),
                "Each page must include the correct bank identity");
        require(has_text(page, "Sida " + std::to_string(i + 1) + " av " + std::to_string(layout->pages.size())),
                "Page totals must be resolved after pagination");
        for (const auto& command : page.commands) {
            if (const auto* text = std::get_if<PositionedText>(&command); text && text->text == "2026-01-15") {
                ++dates;
            }
        }
    }
    require(dates == 100, "Pagination must not drop or duplicate transactions");
}

void test_oversized_content() {
    auto stmt = statement();
    stmt.title = std::string(2000, 'W');
    stmt.account_name = std::string(1000, 'W');
    stmt.transactions.push_back(StatementTransaction{
        .date = "2026-01-15",
        .type = "Insättning",
        .description = std::string(10000, 'Z'),
        .amount_display = "123 456 789 012 345,67 SEK",
        .balance_after_display = "987 654 321 012 345,67 SEK",
    });
    const auto layout = LayoutBuilder::build_statement(stmt);
    require(layout.has_value() && layout->pages.size() > 3, "Oversized cells must span pages");
    check_bounds(*layout);
    size_t description_characters = 0;
    for (const auto& page : layout->pages) {
        for (const auto& command : page.commands) {
            if (const auto* text = std::get_if<PositionedText>(&command)) {
                description_characters += std::ranges::count(text->text, 'Z');
            }
        }
    }
    require(description_characters == 10000, "Split rows must retain the entire description");

    AnnualTaxReport tax{.tax_year = "2025",
                        .account_number = "DEMO-10001",
                        .account_name = "Nordiska Flex",
                        .total_interest_earned = "4 375,00 SEK",
                        .preliminary_tax_deducted = "1 312,50 SEK",
                        .reported_to_authority = std::string(5000, 'W')};
    auto annual = LayoutBuilder::build_tax_report(tax);
    require(annual.has_value(), "Annual layout must succeed");
    check_bounds(*annual);
    require(has_text(annual->pages.front(), "Kontrolluppgift"), "Annual report must have its own heading");
    require(has_text(annual->pages.front(), "Total intjänad ränta"), "Annual report must show its financial summary");
}

void test_renderers_and_resource_reuse() {
    require(nordiska_logo().rgb[0] == 255 && nordiska_logo().rgb[1] == 255 && nordiska_logo().rgb[2] == 255,
            "Transparent source logo background must be flattened onto white paper");
    auto stmt = statement();
    stmt.transactions.resize(50, StatementTransaction{.date = "2026-01-15",
                                                      .description = "ÅÄÖ åäö insättning",
                                                      .amount_display = "100,00 SEK",
                                                      .balance_after_display = "500,00 SEK"});
    auto layout = LayoutBuilder::build_statement(stmt);
    require(layout.has_value(), "Synthetic layout must succeed");
    for (auto kind : {PdfEngineKind::Native, PdfEngineKind::Libharu, PdfEngineKind::Cairo}) {
        PdfEngine engine(kind, false);
        auto pdf = engine.render(*layout);
        require(pdf.has_value(), "Every backend must support branded drawing commands");
        require(nordiska::test::validate_pdf_with_ghostscript(*pdf), "Branded PDF must pass Ghostscript");
        if (kind == PdfEngineKind::Native) {
            const std::string bytes(pdf->begin(), pdf->end());
            const auto image = bytes.find("/Subtype /Image");
            require(image != std::string::npos && bytes.find("/Subtype /Image", image + 1) == std::string::npos,
                    "Repeated logos must share one PDF image object");
            require(bytes.find(" re f") != std::string::npos, "Title band must render as a vector rectangle");
        }
        DocumentLayout invalid{.pages = {{.commands = {PositionedImage{0, 0, 10, 10, nullptr}}}}};
        require(!engine.render(invalid), "Null image resources must return an error");
    }
}
} // namespace

int main() {
    using namespace std::string_view_literals;
    test_wrapping();
    test_branding_and_continuations();
    test_oversized_content();
    test_renderers_and_resource_reuse();
    std::cout << "Report layout tests passed\n";
}
