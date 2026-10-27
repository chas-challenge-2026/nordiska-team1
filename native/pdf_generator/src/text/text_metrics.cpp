#include "nordiska/text/text_metrics.hpp"

#include "nordiska/text/encoding.hpp"

#include <algorithm>
#include <array>
#include <cstdint>

namespace nordiska {
namespace {
#include "helvetica_metrics.inc"

constexpr std::array<uint32_t, 32> kWinAnsiSpecial = {
    0x20AC, '?',    0x201A, 0x0192, 0x201E, 0x2026, 0x2020, 0x2021, 0x02C6, 0x2030, 0x0160,
    0x2039, 0x0152, '?',    0x017D, '?',    '?',    0x2018, 0x2019, 0x201C, 0x201D, 0x2022,
    0x2013, 0x2014, 0x02DC, 0x2122, 0x0161, 0x203A, 0x0153, '?',    0x017E, 0x0178,
};

std::string to_utf8(std::string_view bytes) {
    std::string result;
    for (unsigned char byte : bytes) {
        const uint32_t cp = byte >= 128 && byte < 160 ? kWinAnsiSpecial[byte - 128] : byte;
        if (cp < 128) {
            result.push_back(static_cast<char>(cp));
        } else if (cp < 2048) {
            result.push_back(static_cast<char>(0xC0 | (cp >> 6)));
            result.push_back(static_cast<char>(0x80 | (cp & 63)));
        } else {
            result.push_back(static_cast<char>(0xE0 | (cp >> 12)));
            result.push_back(static_cast<char>(0x80 | ((cp >> 6) & 63)));
            result.push_back(static_cast<char>(0x80 | (cp & 63)));
        }
    }
    return result;
}

std::string encoded_text(std::string_view text) {
    std::string encoded;
    utf8_to_cp1252_append(text, encoded);
    for (char& c : encoded) {
        if (static_cast<unsigned char>(c) < 32 && c != '\n') {
            c = ' ';
        }
    }
    return encoded;
}

float advance(unsigned char c, float size, FontWeight weight) {
    const auto& widths = weight == FontWeight::Bold ? kBoldWidths : kNormalWidths;
    return widths[c] * size / 1000.0F;
}
} // namespace

std::string display_text(std::string_view text) {
    return to_utf8(encoded_text(text));
}

float text_width(std::string_view text, float size, FontWeight weight) {
    float result = 0;
    for (unsigned char c : encoded_text(text)) {
        result += advance(c, size, weight);
    }
    return result;
}

std::vector<std::string> wrap_text(std::string_view text, float width, float size, FontWeight weight) {
    const std::string encoded = encoded_text(text);
    std::vector<std::string> lines;
    size_t begin = 0;
    while (begin < encoded.size()) {
        float used = 0;
        size_t end = begin;
        size_t last_space = std::string::npos;
        while (end < encoded.size() && encoded[end] != '\n') {
            const float next = advance(static_cast<unsigned char>(encoded[end]), size, weight);
            if (used + next > width && end > begin) {
                break;
            }
            used += next;
            if (encoded[end] == ' ') {
                last_space = end;
            }
            ++end;
        }
        const bool newline = end < encoded.size() && encoded[end] == '\n';
        if (!newline && end < encoded.size() && last_space != std::string::npos && last_space > begin) {
            end = last_space;
        }
        lines.push_back(to_utf8(std::string_view(encoded).substr(begin, end - begin)));
        begin = end;
        if (newline) {
            ++begin;
        }
        while (begin < encoded.size() && encoded[begin] == ' ') {
            ++begin;
        }
    }
    if (lines.empty()) {
        lines.emplace_back();
    }
    return lines;
}
} // namespace nordiska
