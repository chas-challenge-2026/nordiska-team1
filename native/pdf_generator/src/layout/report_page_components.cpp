#include "report_page_components.hpp"

#include "nordiska/text/text_metrics.hpp"

#include <array>

namespace nordiska::report {
namespace {
void text(PageLayout& page, float x, float y, std::string_view value, TextStyle typography, Color color) {
    page.commands.emplace_back(PositionedText{x, y, display_text(value), typography.size, typography.weight, color});
}
} // namespace

void append_header(PageLayout& page, const ReportStyle& style, bool first_page) {
    page.commands.emplace_back(
        PositionedImage{style.left, 35.4F, 114.8F, 19.84F, &nordiska_logo(style.report_profile)});
    if (first_page) {
        constexpr std::array<std::string_view, 3> sender = {"Nordiska Bankaktiebolaget (publ)", "Box 173",
                                                            "101 23 Stockholm"};
        float y = 124.92F;
        for (const auto line : sender) {
            text(page, 292.15F, y, line, style.body, style.ink);
            y += style.line_height;
        }
    }
}

void append_recipient(PageLayout& page, const ReportStyle& style, std::span<const std::string> name_lines) {
    float y = 124.92F;
    const auto line = [&](std::string_view value) {
        text(page, style.left + 5.4F, y, value, style.body, style.ink);
        y += style.line_height;
    };
    for (const auto& name : name_lines) {
        line(name);
    }
    // Explicitly requested placeholder until the input contract supplies addresses.
    for (const auto value : {"Gatnamnet 32", "123 45 Staden", "SE"}) {
        line(value);
    }
}

bool append_title_band(PageLayout& page, const ReportStyle& style, float top, std::string_view heading,
                       std::string_view subtitle) {
    page.commands.emplace_back(FilledRectangle{0, top, style.page_width, 85.15F, style.ink});
    const float title_y = top + 34.65F;
    text(page, style.left + 5, title_y, heading, style.title, style.paper);
    const float dot_x = style.left + 5 + text_width(heading, style.title.size, style.title.weight) + 2;
    text(page, dot_x, title_y, ".", style.title, style.title_accent);
    if (subtitle.find('\n') != std::string_view::npos ||
        text_width(subtitle, style.subtitle.size, style.subtitle.weight) > style.content_width() - 10) {
        return false;
    }
    text(page, style.left + 5, top + 52.15F, subtitle, style.subtitle, style.paper);
    return true;
}

void append_footer(PageLayout& page, const ReportStyle& style, size_t page_number, size_t page_count) {
    constexpr std::string_view bank = "Nordiska Bankaktiebolaget |";
    constexpr std::string_view organization = " Organisationsnummer: 556760-6032";
    constexpr std::array<std::string_view, 3> footer = {
        "Postadress: Box 173, 101 23 Stockholm",
        "Besöksadress: Riddargatan 10, 114 35 Stockholm",
        "Telefon: 08-23 28 00, info@nordiska.com, www.nordiska.com",
    };
    const float bank_width = text_width(bank, style.footer.size, FontWeight::Bold);
    const float organization_width = text_width(organization, style.footer.size, style.footer.weight);
    const float identity_x = (style.page_width - bank_width - organization_width) / 2;
    text(page, identity_x, style.footer_y, bank, {style.footer.size, FontWeight::Bold}, style.ink);
    text(page, identity_x + bank_width, style.footer_y, organization, style.footer, style.ink);
    float y = style.footer_y + style.footer_line_height;
    for (const auto line : footer) {
        const float x = (style.page_width - text_width(line, style.footer.size, style.footer.weight)) / 2;
        text(page, x, y, line, style.footer, style.ink);
        y += style.footer_line_height;
    }
    const std::string number = "Sida " + std::to_string(page_number) + " av " + std::to_string(page_count);
    text(page, style.right - text_width(number, 7), 782, number, {7}, style.ink);
}
} // namespace nordiska::report
