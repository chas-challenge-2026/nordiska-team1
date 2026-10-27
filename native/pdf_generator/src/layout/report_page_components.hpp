#pragma once

#include "nordiska/layout/report_style.hpp"

#include <cstddef>
#include <span>
#include <string>
#include <string_view>

namespace nordiska::report {

// Shared page decoration emits only IR commands and has no flow or document state.
void append_header(PageLayout& page, const ReportStyle& style, bool first_page);
void append_recipient(PageLayout& page, const ReportStyle& style, std::span<const std::string> name_lines);
// Returns whether the subtitle fitted inside the band; the composer flows long subtitles.
[[nodiscard]] bool append_title_band(PageLayout& page, const ReportStyle& style, float top, std::string_view heading,
                                     std::string_view subtitle);
void append_footer(PageLayout& page, const ReportStyle& style, size_t page_number, size_t page_count);

} // namespace nordiska::report
