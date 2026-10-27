#include "report_components.hpp"

#include "nordiska/text/text_metrics.hpp"

#include <algorithm>
#include <array>
#include <utility>

namespace nordiska::report {
namespace {
constexpr float kFirstBandY = 221.84F;
constexpr float kContinuationBandY = 83.0F;
constexpr float kBodyOffset = 130.52F;
constexpr float kTableHeaderHeight = 21.0F;
constexpr float kRowPadding = 8.0F;
} // namespace

Composer::Composer(std::string_view heading, std::string_view subtitle, std::string_view recipient,
                   const ReportStyle& style)
    : style_(style), heading_(heading), subtitle_(subtitle) {
    new_page();
    const auto name_lines = wrap_text(recipient, 200, style_.body.size);
    const bool name_fits = name_lines.size() <= 3;
    auto& first_page = document_.pages.front();
    float baseline = 124.92F;
    const auto address_line = [&](std::string_view value) {
        first_page.commands.emplace_back(PositionedText{style_.left + 5.4F, baseline, display_text(value),
                                                        style_.body.size, style_.body.weight, style_.ink});
        baseline += style_.line_height;
    };
    if (name_fits) {
        for (const auto& line : name_lines) {
            address_line(line);
        }
    }
    // Deliberate placeholder requested for every recipient until address data
    // is part of the input contract. Never copy an address from the reference.
    for (const auto line : {"Gatnamnet 32", "123 45 Staden", "SE"}) {
        address_line(line);
    }
    if (!name_fits) {
        paragraph(recipient);
        gap(10);
    }
}

void Composer::text(float x, float y, std::string_view value, float size, FontWeight weight, Color color) {
    document_.pages.back().commands.emplace_back(PositionedText{x, y, display_text(value), size, weight, color});
}

void Composer::rule(float y, float width, Color color) {
    document_.pages.back().commands.emplace_back(PositionedLine{style_.left, y, style_.right, y, width, color});
}

void Composer::new_page() {
    const bool first = document_.pages.empty();
    document_.pages.push_back(PageLayout{style_.page_width, style_.page_height, {}});
    auto& page = document_.pages.back();
    page.commands.emplace_back(PositionedImage{style_.left, 35.4F, 114.8F, 19.84F, &nordiska_logo()});
    if (first) {
        text(292.15F, 124.92F, "Nordiska Bankaktiebolaget (publ)", style_.body.size, style_.body.weight, style_.ink);
        text(292.15F, 124.92F + style_.line_height, "Box 173", style_.body.size, style_.body.weight, style_.ink);
        text(292.15F, 124.92F + 2 * style_.line_height, "101 23 Stockholm", style_.body.size, style_.body.weight,
             style_.ink);
    }
    const float band_y = first ? kFirstBandY : kContinuationBandY;
    page.commands.emplace_back(FilledRectangle{0, band_y, style_.page_width, 85.15F, style_.ink});
    const float title_y = band_y + 34.65F;
    text(style_.left + 5, title_y, heading_, style_.title.size, style_.title.weight, style_.paper);
    const float dot_x = style_.left + 5 + text_width(heading_, style_.title.size, style_.title.weight) + 2;
    text(dot_x, title_y, ".", style_.title.size, style_.title.weight, style_.accent);
    // Subtitles may be arbitrarily long; flow their full content below the band
    // rather than allowing metadata to collide with the title or page edge.
    y_ = band_y + kBodyOffset;
    if (!subtitle_.empty() && subtitle_.find('\n') == std::string::npos &&
        text_width(subtitle_, style_.subtitle.size, style_.subtitle.weight) <= style_.right - style_.left - 10) {
        text(style_.left + 5, band_y + 52.15F, subtitle_, style_.subtitle.size, style_.subtitle.weight, style_.paper);
    } else if (first && !subtitle_.empty()) {
        paragraph(subtitle_, style_.subtitle);
        gap(8);
    }
    if (!columns_.empty()) {
        table_header();
    }
}

void Composer::paragraph(std::string_view value) {
    paragraph(value, style_.body);
}

void Composer::section_heading(std::string_view value) {
    paragraph(value, style_.section_heading);
}

void Composer::field(std::string_view label, std::string_view value) {
    const float label_width = text_width(label, style_.body.size, FontWeight::Bold);
    const auto lines = wrap_text(value, style_.right - style_.left - label_width, style_.body.size, style_.body.weight);
    for (size_t i = 0; i < lines.size(); ++i) {
        if (y_ + style_.line_height > style_.content_bottom) {
            new_page();
        }
        if (i == 0) {
            text(style_.left, y_, label, style_.body.size, FontWeight::Bold, style_.ink);
        }
        text(style_.left + label_width, y_, lines[i], style_.body.size, style_.body.weight, style_.ink);
        y_ += style_.line_height;
    }
}

void Composer::paragraph(std::string_view value, TextStyle typography) {
    const auto [size, weight] = typography;
    const auto lines = wrap_text(value, style_.right - style_.left, size, weight);
    const float leading = std::max(style_.line_height, size * 1.2F);
    for (const auto& line : lines) {
        if (y_ + leading > style_.content_bottom) {
            new_page();
        }
        text(style_.left, y_, line, size, weight, style_.ink);
        y_ += leading;
    }
}

void Composer::gap(float points) {
    y_ += points;
}

void Composer::begin_table(std::span<const Column> columns) {
    columns_.assign(columns.begin(), columns.end());
    if (y_ + 45 > style_.content_bottom) {
        new_page();
    } else {
        table_header();
    }
}

void Composer::table_header() {
    float x = style_.left;
    for (const auto& column : columns_) {
        const float label_x = column.right_aligned ? x + column.width - 6 -
                                                         text_width(column.heading, style_.table_heading.size,
                                                                    style_.table_heading.weight)
                                                   : x + 5;
        text(label_x, y_, column.heading, style_.table_heading.size, style_.table_heading.weight, style_.ink);
        x += column.width;
    }
    rule(y_ + 5, 1.25F, style_.ink);
    y_ += kTableHeaderHeight;
}

void Composer::row(std::span<const std::string> cells, FontWeight weight) {
    std::vector<std::vector<std::string>> wrapped;
    size_t line_count = 1;
    for (size_t i = 0; i < columns_.size(); ++i) {
        wrapped.push_back(
            wrap_text(i < cells.size() ? cells[i] : "", columns_[i].width - 12, style_.body.size, weight));
        line_count = std::max(line_count, wrapped.back().size());
    }
    const float height = static_cast<float>(line_count) * style_.line_height + kRowPadding;
    // Keep normal rows together. Oversized rows are split at line boundaries.
    const float continuation_capacity = style_.content_bottom - (kContinuationBandY + kBodyOffset + kTableHeaderHeight);
    if (y_ + height > style_.content_bottom && height <= continuation_capacity) {
        new_page();
    }
    for (size_t line = 0; line < line_count; ++line) {
        if (y_ + style_.line_height + kRowPadding > style_.content_bottom) {
            rule(y_ - 5, 0.4F, style_.rule);
            new_page();
        }
        float x = style_.left;
        for (size_t cell = 0; cell < columns_.size(); ++cell) {
            if (line < wrapped[cell].size()) {
                const auto& value = wrapped[cell][line];
                const float cell_x = columns_[cell].right_aligned
                                         ? x + columns_[cell].width - 6 - text_width(value, style_.body.size, weight)
                                         : x + 5;
                text(cell_x, y_, value, style_.body.size, weight, style_.ink);
            }
            x += columns_[cell].width;
        }
        y_ += style_.line_height;
    }
    rule(y_ - style_.line_height + 6, 0.4F, style_.rule);
    y_ += kRowPadding;
}

void Composer::end_table() {
    columns_.clear();
    gap(12);
}

DocumentLayout Composer::finish() {
    constexpr std::string_view bank = "Nordiska Bankaktiebolaget |";
    constexpr std::string_view organization = " Organisationsnummer: 556760-6032";
    constexpr std::array<std::string_view, 3> footer = {
        "Postadress: Box 173, 101 23 Stockholm",
        "Besöksadress: Riddargatan 10, 114 35 Stockholm",
        "Telefon: 08-23 28 00, info@nordiska.com, www.nordiska.com",
    };
    for (size_t i = 0; i < document_.pages.size(); ++i) {
        auto& page = document_.pages[i];
        const float bank_width = text_width(bank, style_.footer.size, FontWeight::Bold);
        const float organization_width = text_width(organization, style_.footer.size, style_.footer.weight);
        const float identity_x = (style_.page_width - bank_width - organization_width) / 2;
        page.commands.emplace_back(PositionedText{identity_x, style_.footer_y, std::string(bank), style_.footer.size,
                                                  FontWeight::Bold, style_.ink});
        page.commands.emplace_back(PositionedText{identity_x + bank_width, style_.footer_y, std::string(organization),
                                                  style_.footer.size, style_.footer.weight, style_.ink});
        float y = style_.footer_y + style_.footer_line_height;
        for (const auto line : footer) {
            const float x = (style_.page_width - text_width(line, style_.footer.size, style_.footer.weight)) / 2;
            page.commands.emplace_back(
                PositionedText{x, y, std::string(line), style_.footer.size, style_.footer.weight, style_.ink});
            y += style_.footer_line_height;
        }
        const std::string number = "Sida " + std::to_string(i + 1) + " av " + std::to_string(document_.pages.size());
        page.commands.emplace_back(
            PositionedText{style_.right - text_width(number, 7), 782, number, 7, FontWeight::Normal, style_.ink});
    }
    return std::move(document_);
}
} // namespace nordiska::report
