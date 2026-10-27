#include "report_components.hpp"

#include "nordiska/text/text_metrics.hpp"
#include "report_page_components.hpp"

#include <algorithm>
#include <cmath>
#include <stdexcept>
#include <utility>

namespace nordiska::report {
namespace {
constexpr float kTableHeaderHeight = 21.0F;
constexpr float kRowPadding = 8.0F;
} // namespace

Composer::Composer(std::string_view heading, std::string_view subtitle, std::string_view recipient,
                   const ReportStyle& style)
    : style_(style), heading_(heading), subtitle_(subtitle) {
    new_page();
    const auto name_lines = wrap_text(recipient, 200, style_.body.size);
    const bool name_fits = name_lines.size() <= 3;
    append_recipient(document_.pages.front(), style_,
                     name_fits ? std::span<const std::string>(name_lines) : std::span<const std::string>{});
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
    append_header(page, style_, first);
    const float band_y = first ? style_.first_band_y : style_.continuation_band_y;
    const bool subtitle_drawn = append_title_band(page, style_, band_y, heading_, subtitle_);
    y_ = band_y + style_.body_offset;
    if (first && !subtitle_.empty() && !subtitle_drawn) {
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
    const auto lines = wrap_text(value, style_.content_width() - label_width, style_.body.size, style_.body.weight);
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
    const auto lines = wrap_text(value, style_.content_width(), size, weight);
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
    if (columns.empty() || std::ranges::any_of(columns, [](const auto& column) {
            return !std::isfinite(column.proportion) || column.proportion <= 0;
        })) {
        throw std::invalid_argument("Table columns must have positive finite proportions");
    }
    double total = 0;
    for (const auto& column : columns) {
        total += column.proportion;
    }
    columns_.clear();
    columns_.reserve(columns.size());
    float x = style_.left;
    for (size_t i = 0; i < columns.size(); ++i) {
        const auto& column = columns[i];
        const float width = i + 1 == columns.size()
                                ? style_.right - x
                                : static_cast<float>(style_.content_width() * column.proportion / total);
        columns_.push_back({column.heading, width, column.right_aligned});
        x += width;
    }
    gap(style_.table_gap);
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
    if (columns_.empty() || cells.size() != columns_.size()) {
        throw std::invalid_argument("Table rows must match the active column count");
    }
    std::vector<std::vector<std::string>> wrapped;
    size_t line_count = 1;
    for (size_t i = 0; i < columns_.size(); ++i) {
        wrapped.push_back(wrap_text(cells[i], columns_[i].width - 12, style_.body.size, weight));
        line_count = std::max(line_count, wrapped.back().size());
    }
    const float height = static_cast<float>(line_count) * style_.line_height + kRowPadding;
    // Keep normal rows together. Oversized rows are split at line boundaries.
    const float continuation_capacity =
        style_.content_bottom - (style_.continuation_band_y + style_.body_offset + kTableHeaderHeight);
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
    for (size_t i = 0; i < document_.pages.size(); ++i) {
        append_footer(document_.pages[i], style_, i + 1, document_.pages.size());
    }
    return std::move(document_);
}
} // namespace nordiska::report
