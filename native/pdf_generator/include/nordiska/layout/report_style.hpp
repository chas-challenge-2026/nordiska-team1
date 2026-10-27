#pragma once

#include "nordiska/layout/document_layout.hpp"

#include <string_view>

namespace nordiska {

// Presentation values only. No customer data or PDF-backend objects live here.
struct TextStyle {
    float size;
    FontWeight weight{FontWeight::Normal};
};

struct ReportStyle {
    std::string_view version{"nordiska-reports-v3"};
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

    Color ink{19.0F / 255, 71.0F / 255, 99.0F / 255};
    Color accent{247.0F / 255, 169.0F / 255, 19.0F / 255};
    Color paper{1, 1, 1};
    Color rule{0.72F, 0.78F, 0.81F};
};

inline constexpr ReportStyle kNordiskaReportStyle{};

// Function-local immutable storage is initialized once per process, including
// stateless C API callers. Layouts borrow pixels with process lifetime.
[[nodiscard]] const RasterImage& nordiska_logo();

} // namespace nordiska
