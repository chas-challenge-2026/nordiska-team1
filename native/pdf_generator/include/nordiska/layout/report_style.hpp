#pragma once

#include "nordiska/layout/document_layout.hpp"

#include <optional>
#include <string_view>

namespace nordiska {

enum class ReportProfile { Amber, LightBlue, Navy };
[[nodiscard]] std::optional<ReportProfile> parse_report_profile(std::string_view value) noexcept;

// Presentation values only. No customer data or PDF-backend objects live here.
struct TextStyle {
    float size;
    FontWeight weight{FontWeight::Normal};
};

struct ReportStyle {
    std::string_view version{"nordiska-reports-v5"};
    ReportProfile report_profile{ReportProfile::LightBlue};
    float page_width{595.3F};
    float page_height{841.9F};
    float left{70.85F};
    float right{524.45F};
    float content_bottom{770.0F};
    float footer_y{792.4F};
    float first_band_y{221.84F};
    float continuation_band_y{83.0F};
    float body_offset{130.52F};
    float table_gap{14.75F};
    TextStyle body{11};
    TextStyle title{22, FontWeight::Bold};
    TextStyle subtitle{12, FontWeight::Bold};
    TextStyle section_heading{13, FontWeight::Bold};
    TextStyle table_heading{11, FontWeight::Bold};
    TextStyle footer{6};
    float line_height{14.84F};
    float footer_line_height{7.5F};
    [[nodiscard]] constexpr float content_width() const noexcept {
        return right - left;
    }

    Color ink{0, 16.0F / 255, 43.0F / 255};
    Color accent{136.0F / 255, 188.0F / 255, 1};
    Color title_accent{accent};
    Color paper{1, 1, 1};
    Color rule{0.8F, 0.8F + 0.2F * ink.green, 0.8F + 0.2F * ink.blue};
};

// One palette owns the logo and every page element, including continuation
// pages. Geometry and typography stay shared across profiles.
[[nodiscard]] constexpr ReportStyle report_style(ReportProfile profile) noexcept {
    ReportStyle style;
    style.report_profile = profile;
    switch (profile) {
    case ReportProfile::Amber:
        style.ink = {19.0F / 255, 71.0F / 255, 99.0F / 255};
        style.accent = {237.0F / 255, 169.0F / 255, 26.0F / 255};
        style.title_accent = style.accent;
        break;
    case ReportProfile::LightBlue:
        break;
    case ReportProfile::Navy:
        style.accent = style.ink;
        // The title sits on a navy band, so its punctuation uses white.
        style.title_accent = style.paper;
        break;
    }
    style.rule = {0.8F + 0.2F * style.ink.red, 0.8F + 0.2F * style.ink.green, 0.8F + 0.2F * style.ink.blue};
    return style;
}
inline constexpr ReportStyle kNordiskaReportStyle = report_style(ReportProfile::LightBlue);

// Function-local immutable storage is initialized once per process, including
// stateless C API callers. Layouts borrow pixels with process lifetime.
[[nodiscard]] const RasterImage& nordiska_logo(ReportProfile variant = ReportProfile::LightBlue);

} // namespace nordiska
