#include "nordiska/layout/layout_builder.hpp"
#include "nordiska/layout/report_style.hpp"
#include "nordiska/rendering/pdf_engine.hpp"
#include "nordiska/text/text_metrics.hpp"
#include "pdf_test_helpers.hpp"
#include "report_components.hpp"

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
        require(has_text(page, "Nordiska Bankaktiebolaget |") && has_text(page, " Organisationsnummer: 556760-6032"),
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

void test_reference_typography_and_dummy_address() {
    const auto check = [](const DocumentLayout& layout, std::string_view heading, std::string_view subtitle) {
        const auto& page = layout.pages.front();
        const auto find = [&](std::string_view value) -> const PositionedText& {
            for (const auto& command : page.commands) {
                if (const auto* text = std::get_if<PositionedText>(&command); text && text->text == value) {
                    return *text;
                }
            }
            throw std::runtime_error("Expected report text is missing");
        };
        const auto& title = find(heading);
        require(title.font_size == 22 && title.weight == FontWeight::Bold,
                "Title must match the reference's 22 pt bold");
        const auto& band_subtitle = find(subtitle);
        require(band_subtitle.font_size == 12 && band_subtitle.weight == FontWeight::Bold,
                "Band subtitle must match the reference's 12 pt bold");
        const auto& recipient = find("Anna Exempel");
        require(recipient.font_size == 11 && recipient.weight == FontWeight::Normal,
                "Recipient must use 11 pt regular text");
        float baseline = recipient.y;
        for (const auto line : {"Gatnamnet 32", "123 45 Staden", "SE"}) {
            const auto& address = find(line);
            require(address.font_size == 11 && address.weight == FontWeight::Normal && address.x == recipient.x,
                    "Every report must use the same dummy address styling and alignment");
            require(std::abs(address.y - baseline - 14.84F) < 0.01F,
                    "Address must follow the reference's line spacing");
            baseline = address.y;
        }
        const auto& label = find("Kontonummer: ");
        const auto& value = find("DEMO-10001");
        require(label.font_size == 11 && label.weight == FontWeight::Bold && value.font_size == 11 &&
                    value.weight == FontWeight::Normal && label.y == value.y,
                "Field labels must be bold and values regular on the same baseline");
        require(std::abs(value.x - label.x - text_width(label.text, 11, FontWeight::Bold)) < 0.01F,
                "Field values must follow measured label widths");
        const auto& table_heading = find("Uppgift");
        require(table_heading.font_size == 11 && table_heading.weight == FontWeight::Bold,
                "Table headings must match the reference's 11 pt bold");
        require(find("Nordiska Bankaktiebolaget |").weight == FontWeight::Bold &&
                    find(" Organisationsnummer: 556760-6032").weight == FontWeight::Normal,
                "Footer must preserve the reference's mixed emphasis");
        check_bounds(layout);
    };
    auto statement_layout = LayoutBuilder::build_statement(statement(), {.customer_name = "Anna Exempel"});
    require(statement_layout.has_value(), "Statement typography fixture must build");
    check(*statement_layout, "Kontoutdrag", "Period: 2026-01-01 – 2026-01-31");
    AnnualTaxReport tax{.tax_year = "2025", .account_number = "DEMO-10001"};
    auto annual_layout = LayoutBuilder::build_tax_report(tax, {.customer_name = "Anna Exempel"});
    require(annual_layout.has_value(), "Annual typography fixture must build");
    check(*annual_layout, "Kontrolluppgift", "Inkomstår: 2025");

    auto long_name = LayoutBuilder::build_statement(statement(), {.customer_name = std::string(2000, 'W')});
    require(long_name.has_value(), "Long recipient name must flow without displacing the dummy address");
    check_bounds(*long_name);
    for (const auto line : {"Gatnamnet 32", "123 45 Staden", "SE"}) {
        require(has_text(long_name->pages.front(), line), "Dummy address must remain on the first page");
    }
}

void test_shared_report_geometry() {
    auto stmt = statement();
    AnnualTaxReport tax{.tax_year = "2025",
                        .account_number = stmt.account_number,
                        .account_name = stmt.account_name,
                        .total_interest_earned = "1 000,00 SEK",
                        .preliminary_tax_deducted = "300,00 SEK",
                        .reported_to_authority = "Skatteverket"};
    auto account = LayoutBuilder::build_statement(stmt, {.customer_name = "Anna Exempel"});
    auto annual = LayoutBuilder::build_tax_report(tax, {.customer_name = "Anna Exempel"});
    require(account.has_value() && annual.has_value(), "Both reports must build");
    const auto& first = account->pages.front();
    const auto& second = annual->pages.front();
    const auto shared_text = [](const PageLayout& page) {
        std::vector<PositionedText> texts;
        for (const auto& command : page.commands) {
            if (const auto* text = std::get_if<PositionedText>(&command);
                text && (text->y < 200 || text->y >= 780 || text->text == "Kontonummer: " ||
                         text->text == "DEMO-10001" || text->text == "Uppgift")) {
                texts.push_back(*text);
            }
        }
        return texts;
    };
    const auto account_text = shared_text(first);
    const auto annual_text = shared_text(second);
    require(account_text.size() == annual_text.size(), "Shared page elements must have the same command count");
    for (size_t i = 0; i < account_text.size(); ++i) {
        const auto& a = account_text[i];
        const auto& b = annual_text[i];
        require(a.text == b.text && a.x == b.x && a.y == b.y && a.font_size == b.font_size && a.weight == b.weight &&
                    a.color == b.color,
                "Shared recipient, sender, fields, table heading and footer must be identical across report types");
    }
    const auto find_text = [](const PageLayout& page, std::string_view value) -> const PositionedText& {
        for (const auto& command : page.commands) {
            if (const auto* text = std::get_if<PositionedText>(&command); text && text->text == value) {
                return *text;
            }
        }
        throw std::runtime_error("Missing report label");
    };
    const auto same_label_style = [&](std::string_view account_label, std::string_view annual_label) {
        const auto& a = find_text(first, account_label);
        const auto& b = find_text(second, annual_label);
        require(a.x == b.x && a.y == b.y && a.font_size == b.font_size && a.weight == b.weight && a.color == b.color,
                "Different report labels must share geometry and typography");
    };
    same_label_style("Kontoutdrag", "Kontrolluppgift");
    same_label_style("Period: 2026-01-01 – 2026-01-31", "Inkomstår: 2025");
    same_label_style("Nordiska Flex", "Kontrolluppgift för ränteinkomst");
    const auto& account_logo = std::get<PositionedImage>(first.commands.front());
    const auto& annual_logo = std::get<PositionedImage>(second.commands.front());
    require(account_logo.image == annual_logo.image && account_logo.x == annual_logo.x &&
                account_logo.y == annual_logo.y && account_logo.width == annual_logo.width &&
                account_logo.height == annual_logo.height,
            "Both reports must use identical logo geometry");
    const auto band = [](const PageLayout& page) -> const FilledRectangle& {
        for (const auto& command : page.commands) {
            if (const auto* rectangle = std::get_if<FilledRectangle>(&command)) {
                return *rectangle;
            }
        }
        throw std::runtime_error("Missing title band");
    };
    const auto& a = band(first);
    const auto& b = band(second);
    require(a.x == b.x && a.y == b.y && a.width == b.width && a.height == b.height && a.color == b.color,
            "Both reports must use identical title band geometry");
    for (const auto* layout : {&*account, &*annual}) {
        size_t rules = 0;
        for (const auto& command : layout->pages.front().commands) {
            if (const auto* line = std::get_if<PositionedLine>(&command)) {
                require(line->x1 == kNordiskaReportStyle.left && line->x2 == kNordiskaReportStyle.right,
                        "Every table must span the shared content width regardless of column count");
                ++rules;
            }
        }
        require(rules > 1, "Each report must contain table rules");
    }
}

void test_table_proportions_follow_content_width() {
    ReportStyle style;
    style.left = 90;
    style.right = 500;
    const auto check = [&](std::span<const report::Column> columns, std::span<const std::string> cells) {
        report::Composer composer("Kontoutdrag", "Test", "Anna Exempel", style);
        composer.begin_table(columns);
        composer.row(cells);
        auto layout = composer.finish();
        for (const auto& command : layout.pages.front().commands) {
            if (const auto* line = std::get_if<PositionedLine>(&command)) {
                require(line->x1 == 90 && line->x2 == 500, "Tables must adapt to the shared content edges");
            }
            if (const auto* text = std::get_if<PositionedText>(&command); text && text->text == "Right edge") {
                require(std::abs(text->x + text_width(text->text, text->font_size, text->weight) - 494) < 0.01F,
                        "Last column must end at the shared right edge minus cell padding");
            }
        }
    };
    check(std::array{report::Column{"First", 1}, report::Column{"Last", 3, true}},
          std::array<std::string, 2>{"Left", "Right edge"});
    check(std::array{report::Column{"First", 4}, report::Column{"Second", 2}, report::Column{"Third", 1},
                     report::Column{"Last", 3, true}},
          std::array<std::string, 4>{"Left", "Middle", "Other", "Right edge"});
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
void test_report_profiles() {
    const auto& original = nordiska_logo(ReportProfile::Amber);
    const auto& blue = nordiska_logo(ReportProfile::LightBlue);
    const auto& navy = nordiska_logo(ReportProfile::Navy);
    require(&nordiska_logo() == &blue, "Default profile must use a light-blue dot");
    bool amber_dot{}, blue_dot{}, navy_ink{};
    for (size_t i = 0; i < original.rgb.size(); i += 3) {
        amber_dot |= original.rgb[i] == 237 && original.rgb[i + 1] == 169 && original.rgb[i + 2] == 26;
        blue_dot |= blue.rgb[i] == 136 && blue.rgb[i + 1] == 188 && blue.rgb[i + 2] == 255;
        navy_ink |= navy.rgb[i] == 0 && navy.rgb[i + 1] == 16 && navy.rgb[i + 2] == 43;
    }
    require(amber_dot && blue_dot && navy_ink, "Logo profile palette is incorrect");
    const Color modern_ink{0, 16.0F / 255, 43.0F / 255};
    require(report_style(ReportProfile::LightBlue).ink == modern_ink, "Blue profile retained legacy text ink");
    require(report_style(ReportProfile::Navy).ink == modern_ink, "Navy profile retained legacy text ink");
    require(report_style(ReportProfile::LightBlue).title_accent == Color{136.0F / 255, 188.0F / 255, 1},
            "Blue title punctuation retained the legacy amber");
    require(report_style(ReportProfile::Navy).title_accent == Color{1, 1, 1},
            "Monochrome title punctuation must contrast with its navy band");
    AccountStatement statement{.account_number = "SYNTHETIC-1", .account_name = "Sparkonto"};
    statement.transactions.resize(90, StatementTransaction{.date = "2026-01-01",
                                                           .description = "Synthetic transaction",
                                                           .amount_display = "10,00 SEK"});
    AnnualTaxReport tax{.tax_year = "2026", .account_number = "SYNTHETIC-1", .account_name = "Sparkonto"};
    for (auto profile : {ReportProfile::Amber, ReportProfile::LightBlue, ReportProfile::Navy}) {
        const auto style = report_style(profile);
        const auto& image = nordiska_logo(profile);
        require(&image == &nordiska_logo(profile), "Profile resources must be cached");
        require(image.rgb[0] == 255 && image.rgb[1] == 255 && image.rgb[2] == 255, "Every logo must use white paper");
        const auto statement_layout = LayoutBuilder::build_statement(statement, {.report_profile = profile});
        const auto tax_layout = LayoutBuilder::build_tax_report(tax, {.report_profile = profile});
        require(statement_layout && tax_layout, "Report profile layout failed");
        require(statement_layout->pages.size() > 1, "Profile test must include continuation pages");
        for (const auto* layout : {&*statement_layout, &*tax_layout}) {
            for (const auto& page : layout->pages) {
                bool saw_image{}, saw_title_dot{}, saw_footer{};
                for (const auto& command : page.commands) {
                    if (const auto* placed = std::get_if<PositionedImage>(&command)) {
                        saw_image = true;
                        require(placed->image == &image, "Page selected the wrong profile logo");
                    } else if (const auto* text = std::get_if<PositionedText>(&command)) {
                        require(text->color == style.ink || text->color == style.paper ||
                                    text->color == style.title_accent,
                                "Page text uses a color outside its profile");
                        if (text->text == ".") {
                            saw_title_dot = true;
                            require(text->color == style.title_accent, "Title dot does not match the profile");
                        }
                        if (text->text == "Nordiska Bankaktiebolaget |") {
                            saw_footer = true;
                            require(text->color == style.ink, "Footer does not match the profile");
                        }
                    } else if (const auto* line = std::get_if<PositionedLine>(&command)) {
                        require(line->color == style.ink || line->color == style.rule,
                                "Table rule does not match the profile");
                    } else if (const auto* rectangle = std::get_if<FilledRectangle>(&command)) {
                        require(rectangle->color == style.ink, "Title band does not match the profile");
                    }
                }
                require(saw_image && saw_title_dot && saw_footer, "Page is missing profile furniture");
            }
            for (auto engine : {PdfEngineKind::Native, PdfEngineKind::Libharu, PdfEngineKind::Cairo}) {
                require(PdfEngine(engine).render(*layout).has_value(), "Renderer rejected a report profile");
            }
        }
    }
    require(!parse_report_profile("white"), "White-on-navy profile must be removed");
    require(!parse_report_profile("PRIVATE_CUSTOMER_VALUE"), "Unknown profile must be rejected");
}

} // namespace

int main() {
    using namespace std::string_view_literals;
    test_report_profiles();
    test_wrapping();
    test_branding_and_continuations();
    test_reference_typography_and_dummy_address();
    test_shared_report_geometry();
    test_table_proportions_follow_content_width();
    test_oversized_content();
    test_renderers_and_resource_reuse();
    std::cout << "Report layout tests passed\n";
}
