#pragma once

#include "nordiska/layout/report_style.hpp"

#include <span>
#include <string>
#include <string_view>
#include <vector>

namespace nordiska::report {

struct Column {
    std::string_view heading;
    float proportion; // Relative share of the common content width.
    bool right_aligned{false};
};

// Owns one document's mutable flow state. Shared branding stays immutable.
// A table can continue across pages, including a row taller than a whole page.
class Composer {
  public:
    Composer(std::string_view heading, std::string_view subtitle, std::string_view recipient,
             const ReportStyle& style = kNordiskaReportStyle);

    void paragraph(std::string_view text);
    void paragraph(std::string_view text, TextStyle typography);
    void section_heading(std::string_view text);
    void field(std::string_view label, std::string_view value);
    void gap(float points);
    void begin_table(std::span<const Column> columns);
    void row(std::span<const std::string> cells, FontWeight weight = FontWeight::Normal);
    void end_table();
    [[nodiscard]] DocumentLayout finish();

  private:
    void new_page();
    void table_header();
    void text(float x, float y, std::string_view value, float size, FontWeight weight, Color color);
    void rule(float y, float width, Color color);
    const ReportStyle style_;
    std::string heading_;
    std::string subtitle_;
    DocumentLayout document_;
    struct ResolvedColumn {
        std::string_view heading;
        float width;
        bool right_aligned;
    };
    std::vector<ResolvedColumn> columns_;
    float y_{};
};

} // namespace nordiska::report
