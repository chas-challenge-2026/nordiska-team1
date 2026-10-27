#pragma once

#include "nordiska/text/font.hpp"

#include <cstdint>
#include <span>
#include <string>
#include <variant>
#include <vector>

namespace nordiska {

struct Color {
    float red{};
    float green{};
    float blue{};
    bool operator==(const Color&) const = default;
};

struct PositionedText {
    float x{};
    float y{}; // Baseline, measured down from the top of the page.
    std::string text;
    float font_size{10.0F};
    FontWeight weight{FontWeight::Normal};
    Color color{};
};

struct PositionedLine {
    float x1{};
    float y1{};
    float x2{};
    float y2{};
    float line_width{1.0F};
    Color color{};
};

struct FilledRectangle {
    float x{};
    float y{};
    float width{};
    float height{};
    Color color{};
};

// Immutable RGB8 pixels. Storage must outlive every layout that references it.
// Optional precompressed pixels let direct PDF backends reuse prepared resources.
struct RasterImage {
    uint32_t width{};
    uint32_t height{};
    std::span<const uint8_t> rgb;
    std::span<const uint8_t> deflated_rgb;
};

struct PositionedImage {
    float x{};
    float y{};
    float width{};
    float height{};
    const RasterImage* image{}; // Borrowed; never a backend document handle.
};

using DrawingCommand = std::variant<PositionedText, PositionedLine, FilledRectangle, PositionedImage>;

struct PageLayout {
    float width{595.3F};
    float height{841.9F};
    std::vector<DrawingCommand> commands; // Painter's order, identical across renderers.
};

struct DocumentLayout {
    std::vector<PageLayout> pages;
};

} // namespace nordiska
