#include "report_components.hpp"

#include "nordiska/text/text_metrics.hpp"

#include <algorithm>
#include <array>
#include <utility>

namespace nordiska::report {
namespace {
constexpr float kFirstBandY = 221.84F;
constexpr float kContinuationBandY = 83.0F;
constexpr float kBodyOffset = 120.0F;
constexpr float kTableHeaderHeight = 21.0F;
constexpr float kRowPadding = 8.0F;
} // namespace

Composer::Composer(std::string_view heading, std::string_view subtitle, std::string_view recipient,
                   const ReportStyle& style)
    : style_(style), heading_(heading), subtitle_(subtitle) {
    new_page();
    const auto name_lines = wrap_text(recipient, 200, style_.body_size);
    if (name_lines.size() <= 5 && document_.pages.size() == 1) {
        float baseline = 125;
        for (const auto& line : name_lines) {
            text(style_.left + 5, baseline, line, style_.body_size, FontWeight::Normal, style_.ink);
            baseline += style_.line_height;
        }
    } else {
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
        text(292, 125, "Nordiska Bankaktiebolaget (publ)", 10, FontWeight::Normal, style_.ink);
        text(292, 139, "Box 173", 10, FontWeight::Normal, style_.ink);
        text(292, 153, "101 23 Stockholm", 10, FontWeight::Normal, style_.ink);
    }
    const float band_y = first ? kFirstBandY : kContinuationBandY;
    page.commands.emplace_back(FilledRectangle{0, band_y, style_.page_width, 85.15F, style_.ink});
    const float title_y = band_y + 34.65F;
    text(style_.left + 5, title_y, heading_, 22, FontWeight::Bold, style_.paper);
    const float dot_x = style_.left + 5 + text_width(heading_, 22, FontWeight::Bold) + 2;
    text(dot_x, title_y, ".", 22, FontWeight::Bold, style_.accent);
    // Subtitles may be arbitrarily long; flow their full content below the band
    // rather than allowing metadata to collide with the title or page edge.
    y_ = band_y + kBodyOffset;
    if (!subtitle_.empty() && subtitle_.find('\n') == std::string::npos &&
        text_width(subtitle_, 10, FontWeight::Bold) <= style_.right - style_.left - 10) {
        text(style_.left + 5, band_y + 53, subtitle_, 10, FontWeight::Bold, style_.paper);
    } else if (first && !subtitle_.empty()) {
        paragraph(subtitle_, 10, FontWeight::Bold);
        gap(8);
    }
    if (!columns_.empty()) {
        table_header();
    }
}

void Composer::paragraph(std::string_view value, float size, FontWeight weight) {
    const auto lines = wrap_text(value, style_.right - style_.left, size, weight);
    const float leading = std::max(style_.line_height, size * 1.3F);
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
        const float label_x =
            column.right_aligned ? x + column.width - 6 - text_width(column.heading, 10, FontWeight::Bold) : x + 5;
        text(label_x, y_, column.heading, 10, FontWeight::Bold, style_.ink);
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
            wrap_text(i < cells.size() ? cells[i] : "", columns_[i].width - 12, style_.body_size, weight));
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
                                         ? x + columns_[cell].width - 6 - text_width(value, style_.body_size, weight)
                                         : x + 5;
                text(cell_x, y_, value, style_.body_size, weight, style_.ink);
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
    constexpr std::array<std::string_view, 4> footer = {
        "Nordiska Bankaktiebolaget | Organisationsnummer: 556760-6032",
        "Postadress: Box 173, 101 23 Stockholm",
        "Besöksadress: Riddargatan 10, 114 35 Stockholm",
        "Telefon: 08-23 28 00, info@nordiska.com, www.nordiska.com",
    };
    for (size_t i = 0; i < document_.pages.size(); ++i) {
        auto& page = document_.pages[i];
        float y = style_.footer_y;
        for (const auto line : footer) {
            const float x = (style_.page_width - text_width(line, 6)) / 2;
            page.commands.emplace_back(PositionedText{x, y, std::string(line), 6, FontWeight::Normal, style_.ink});
            y += 8;
        }
        const std::string number = "Sida " + std::to_string(i + 1) + " av " + std::to_string(document_.pages.size());
        page.commands.emplace_back(
            PositionedText{style_.right - text_width(number, 7), 782, number, 7, FontWeight::Normal, style_.ink});
    }
    return std::move(document_);
}
} // namespace nordiska::report
