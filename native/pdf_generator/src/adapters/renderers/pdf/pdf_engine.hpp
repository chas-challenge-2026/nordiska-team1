#pragma once

#include "nordiska/ports/byte_sink.hpp"

#include <memory>
#include <string>
#include <vector>

namespace nordiska::pdf::detail {

enum class TextStyle {
    title,
    body,
};

struct TextLine {
    std::string text;
    TextStyle style{TextStyle::body};
};

struct Page {
    std::vector<TextLine> lines;
};

struct Document {
    std::vector<Page> pages;
};

class IPdfEngine {
  public:
    virtual ~IPdfEngine() = default;
    virtual void render(const Document& document, IByteSink& sink) = 0;
};

std::unique_ptr<IPdfEngine> make_haru_engine();
std::unique_ptr<IPdfEngine> make_cairo_engine();

} // namespace nordiska::pdf::detail
