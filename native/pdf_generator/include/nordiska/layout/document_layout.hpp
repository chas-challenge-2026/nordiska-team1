#pragma once

#include <cstdint>
#include <string>
#include <vector>

namespace nordiska {

enum class FontWeight {
    Normal,
    Bold,
};

struct PositionedText {
    float x{0.0F};
    float y{0.0F};
    std::string text;
    float font_size{10.0F};
    FontWeight weight{FontWeight::Normal};
};

struct PositionedLine {
    float x1{0.0F};
    float y1{0.0F};
    float x2{0.0F};
    float y2{0.0F};
    float line_width{1.0F};
};

struct PageLayout {
    float width{612.0F};
    float height{792.0F};
    std::vector<PositionedText> texts;
    std::vector<PositionedLine> lines;
};

struct DocumentLayout {
    std::vector<PageLayout> pages;
};

} // namespace nordiska
