#pragma once

#include "nordiska/text/font.hpp"

#include <string>
#include <string_view>
#include <vector>

namespace nordiska {

// Helvetica / Helvetica-Bold advance widths, in points. The text is normalized
// through the same WinAnsi mapping used by the PDF backends.
[[nodiscard]] float text_width(std::string_view text, float size, FontWeight weight = FontWeight::Normal);
[[nodiscard]] std::vector<std::string> wrap_text(std::string_view text, float width, float size,
                                                 FontWeight weight = FontWeight::Normal);
[[nodiscard]] std::string display_text(std::string_view text);

} // namespace nordiska
