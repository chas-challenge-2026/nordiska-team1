#pragma once

#include <cstddef>
#include <cstdint>
#include <optional>
#include <string>
#include <string_view>

namespace nordiska {

inline bool is_pure_ascii(std::string_view sv) noexcept {
    for (unsigned char c : sv) {
        if (c >= 0x80) {
            return false;
        }
    }
    return true;
}

inline void append_pdf_char(std::string& dest, char c) noexcept {
    if (c == '(' || c == ')' || c == '\\') {
        dest.push_back('\\');
    }
    dest.push_back(c);
}

inline uint32_t decode_next_utf8_codepoint(std::string_view utf8, std::size_t& index) noexcept {
    const auto b0 = static_cast<unsigned char>(utf8[index]);
    if (b0 < 0x80) {
        ++index;
        return b0;
    }
    if ((b0 & 0xE0) == 0xC0) {
        if (index + 1 < utf8.size()) {
            const auto b1 = static_cast<unsigned char>(utf8[index + 1]);
            if ((b1 & 0xC0) == 0x80) {
                index += 2;
                return ((b0 & 0x1F) << 6) | (b1 & 0x3F);
            }
        }
    } else if ((b0 & 0xF0) == 0xE0) {
        if (index + 2 < utf8.size()) {
            const auto b1 = static_cast<unsigned char>(utf8[index + 1]);
            const auto b2 = static_cast<unsigned char>(utf8[index + 2]);
            if ((b1 & 0xC0) == 0x80 && (b2 & 0xC0) == 0x80) {
                index += 3;
                return ((b0 & 0x0F) << 12) | ((b1 & 0x3F) << 6) | (b2 & 0x3F);
            }
        }
    } else if ((b0 & 0xF8) == 0xF0) {
        if (index + 3 < utf8.size()) {
            const auto b1 = static_cast<unsigned char>(utf8[index + 1]);
            const auto b2 = static_cast<unsigned char>(utf8[index + 2]);
            const auto b3 = static_cast<unsigned char>(utf8[index + 3]);
            if ((b1 & 0xC0) == 0x80 && (b2 & 0xC0) == 0x80 && (b3 & 0xC0) == 0x80) {
                index += 4;
                return ((b0 & 0x07) << 18) | ((b1 & 0x3F) << 12) | ((b2 & 0x3F) << 6) | (b3 & 0x3F);
            }
        }
    }

    // Malformed/unrecognized lead byte or truncated sequence:
    // advance past lead byte and any continuation bytes (0x80..0xBF)
    ++index;
    while (index < utf8.size() && (static_cast<unsigned char>(utf8[index]) & 0xC0) == 0x80) {
        ++index;
    }
    return 0xFFFD;
}

inline std::optional<char> map_codepoint_to_cp1252(uint32_t cp) noexcept {
    if (cp < 0x80) {
        return static_cast<char>(cp);
    }
    // Latin-1 Supplement (U+00A0 - U+00FF): identical to CP1252 byte value
    // (includes å, ä, ö, Å, Ä, Ö, é, §, ©, ®, etc.)
    if (cp >= 0x00A0 && cp <= 0x00FF) {
        return static_cast<char>(cp);
    }
    switch (cp) {
    // Unicode minus & hyphens -> ASCII '-'
    case 0x2212: // MINUS SIGN (−) - NOR-214: used by .NET sv-SE for negative currency
    case 0x2010: // HYPHEN
    case 0x2011: // NON-BREAKING HYPHEN
    case 0x2012: // FIGURE DASH
        return '-';

    // Dashes in CP1252
    case 0x2013: // EN DASH (–)
        return static_cast<char>(0x96);
    case 0x2014: // EM DASH (—)
    case 0x2015: // HORIZONTAL BAR
        return static_cast<char>(0x97);

    // Whitespace and digit grouping (.NET sv-SE ICU uses U+202F for thousands separators)
    case 0x202F: // NARROW NO-BREAK SPACE
    case 0x2007: // FIGURE SPACE
    case 0x2008: // PUNCTUATION SPACE
    case 0x2009: // THIN SPACE
    case 0x200A: // HAIR SPACE
        return ' ';

    // Quotes and apostrophes
    case 0x2018: // LEFT SINGLE QUOTATION MARK (‘)
        return static_cast<char>(0x91);
    case 0x2019: // RIGHT SINGLE QUOTATION MARK / APOSTROPHE (’)
        return static_cast<char>(0x92);
    case 0x201A: // SINGLE LOW-9 QUOTATION MARK (‚)
        return static_cast<char>(0x82);
    case 0x201C: // LEFT DOUBLE QUOTATION MARK (“)
        return static_cast<char>(0x93);
    case 0x201D: // RIGHT DOUBLE QUOTATION MARK (”) - standard Swedish quotes
        return static_cast<char>(0x94);
    case 0x201E: // DOUBLE LOW-9 QUOTATION MARK („)
        return static_cast<char>(0x84);
    case 0x2039: // SINGLE LEFT-POINTING ANGLE QUOTATION MARK (‹)
        return static_cast<char>(0x8B);
    case 0x203A: // SINGLE RIGHT-POINTING ANGLE QUOTATION MARK (›)
        return static_cast<char>(0x9B);

    // Currency & common symbols
    case 0x20AC: // EURO SIGN (€)
        return static_cast<char>(0x80);
    case 0x2022: // BULLET (•)
        return static_cast<char>(0x95);
    case 0x2026: // HORIZONTAL ELLIPSIS (…)
        return static_cast<char>(0x85);
    case 0x2030: // PER MILLE SIGN (‰)
        return static_cast<char>(0x89);
    case 0x2122: // TRADE MARK SIGN (™)
        return static_cast<char>(0x99);

    // Extended Latin characters in CP-1252 (0x80-0x9F)
    case 0x0152: // LATIN CAPITAL LIGATURE OE (Œ)
        return static_cast<char>(0x8C);
    case 0x0153: // LATIN SMALL LIGATURE OE (œ)
        return static_cast<char>(0x9C);
    case 0x0160: // LATIN CAPITAL LETTER S WITH CARON (Š)
        return static_cast<char>(0x8A);
    case 0x0161: // LATIN SMALL LETTER S WITH CARON (š)
        return static_cast<char>(0x9A);
    case 0x0178: // LATIN CAPITAL LETTER Y WITH DIAERESIS (Ÿ)
        return static_cast<char>(0x9F);
    case 0x017D: // LATIN CAPITAL LETTER Z WITH CARON (Ž)
        return static_cast<char>(0x8E);
    case 0x017E: // LATIN SMALL LETTER Z WITH CARON (ž)
        return static_cast<char>(0x9E);
    case 0x0192: // LATIN SMALL LETTER F WITH HOOK (ƒ)
        return static_cast<char>(0x83);
    case 0x02C6: // MODIFIER LETTER CIRCUMFLEX ACCENT (ˆ)
        return static_cast<char>(0x88);
    case 0x02DC: // SMALL TILDE (˜)
        return static_cast<char>(0x98);
    case 0x2020: // DAGGER (†)
        return static_cast<char>(0x86);
    case 0x2021: // DOUBLE DAGGER (‡)
        return static_cast<char>(0x87);

    // Transliteration fallbacks for common European letters outside CP1252
    case 0x0106: // LATIN CAPITAL LETTER C WITH ACUTE (Ć)
    case 0x010C: // LATIN CAPITAL LETTER C WITH CARON (Č)
        return 'C';
    case 0x0107: // LATIN SMALL LETTER C WITH ACUTE (ć)
    case 0x010D: // LATIN SMALL LETTER C WITH CARON (č)
        return 'c';
    case 0x010E: // LATIN CAPITAL LETTER D WITH CARON (Ď)
    case 0x0110: // LATIN CAPITAL LETTER D WITH STROKE (Đ)
        return 'D';
    case 0x010F: // LATIN SMALL LETTER D WITH CARON (ď)
    case 0x0111: // LATIN SMALL LETTER D WITH STROKE (đ)
        return 'd';
    case 0x0141: // LATIN CAPITAL LETTER L WITH STROKE (Ł)
        return 'L';
    case 0x0142: // LATIN SMALL LETTER L WITH STROKE (ł)
        return 'l';
    case 0x0143: // LATIN CAPITAL LETTER N WITH ACUTE (Ń)
    case 0x0147: // LATIN CAPITAL LETTER N WITH CARON (Ň)
        return 'N';
    case 0x0144: // LATIN SMALL LETTER N WITH ACUTE (ń)
    case 0x0148: // LATIN SMALL LETTER N WITH CARON (ň)
        return 'n';
    case 0x0158: // LATIN CAPITAL LETTER R WITH CARON (Ř)
        return 'R';
    case 0x0159: // LATIN SMALL LETTER R WITH CARON (ř)
        return 'r';
    case 0x015A: // LATIN CAPITAL LETTER S WITH ACUTE (Ś)
    case 0x015E: // LATIN CAPITAL LETTER S WITH CEDILLA (Ş)
        return 'S';
    case 0x015B: // LATIN SMALL LETTER S WITH ACUTE (ś)
    case 0x015F: // LATIN SMALL LETTER S WITH CEDILLA (ş)
        return 's';
    case 0x0164: // LATIN CAPITAL LETTER T WITH CARON (Ť)
        return 'T';
    case 0x0165: // LATIN SMALL LETTER T WITH CARON (ť)
        return 't';
    case 0x0179: // LATIN CAPITAL LETTER Z WITH ACUTE (Ź)
    case 0x017B: // LATIN CAPITAL LETTER Z WITH DOT ABOVE (Ż)
        return 'Z';
    case 0x017A: // LATIN SMALL LETTER Z WITH ACUTE (ź)
    case 0x017C: // LATIN SMALL LETTER Z WITH DOT ABOVE (ż)
        return 'z';
    case 0x1E9E: // LATIN CAPITAL LETTER SHARP S (ẞ) -> ß (0xDF)
        return static_cast<char>(0xDF);

    // Invisible / formatting zero-width characters (silently dropped)
    case 0x200B: // ZERO WIDTH SPACE
    case 0x200C: // ZERO WIDTH NON-JOINER
    case 0x200D: // ZERO WIDTH JOINER
    case 0xFEFF: // ZERO WIDTH NO-BREAK SPACE / BYTE ORDER MARK
        return std::nullopt;

    default:
        return '?';
    }
}

constexpr bool is_combining_mark(uint32_t cp) noexcept {
    return (cp >= 0x0300 && cp <= 0x036F) || (cp >= 0x1DC0 && cp <= 0x1DFF) || (cp >= 0x20D0 && cp <= 0x20FF) ||
           (cp >= 0xFE20 && cp <= 0xFE2F);
}

inline uint32_t compose_latin_pair(uint32_t base, uint32_t combining) noexcept {
    switch (combining) {
    case 0x0300: // COMBINING GRAVE ACCENT
        switch (base) {
        case 'A':
            return 0x00C0;
        case 'E':
            return 0x00C8;
        case 'I':
            return 0x00CC;
        case 'O':
            return 0x00D2;
        case 'U':
            return 0x00D9;
        case 'a':
            return 0x00E0;
        case 'e':
            return 0x00E8;
        case 'i':
            return 0x00EC;
        case 'o':
            return 0x00F2;
        case 'u':
            return 0x00F9;
        default:
            return 0;
        }
    case 0x0301: // COMBINING ACUTE ACCENT
        switch (base) {
        case 'A':
            return 0x00C1;
        case 'E':
            return 0x00C9;
        case 'I':
            return 0x00CD;
        case 'O':
            return 0x00D3;
        case 'U':
            return 0x00DA;
        case 'Y':
            return 0x00DD;
        case 'a':
            return 0x00E1;
        case 'e':
            return 0x00E9;
        case 'i':
            return 0x00ED;
        case 'o':
            return 0x00F3;
        case 'u':
            return 0x00FA;
        case 'y':
            return 0x00FD;
        case 'C':
        case 'c':
        case 'N':
        case 'n':
        case 'S':
        case 's':
        case 'Z':
        case 'z':
            return base; // Latin transliteration
        default:
            return 0;
        }
    case 0x0302: // COMBINING CIRCUMFLEX ACCENT
        switch (base) {
        case 'A':
            return 0x00C2;
        case 'E':
            return 0x00CA;
        case 'I':
            return 0x00CE;
        case 'O':
            return 0x00D4;
        case 'U':
            return 0x00DB;
        case 'a':
            return 0x00E2;
        case 'e':
            return 0x00EA;
        case 'i':
            return 0x00EE;
        case 'o':
            return 0x00F4;
        case 'u':
            return 0x00FB;
        default:
            return 0;
        }
    case 0x0303: // COMBINING TILDE
        switch (base) {
        case 'A':
            return 0x00C3;
        case 'N':
            return 0x00D1;
        case 'O':
            return 0x00D5;
        case 'a':
            return 0x00E3;
        case 'n':
            return 0x00F1;
        case 'o':
            return 0x00F5;
        default:
            return 0;
        }
    case 0x0308: // COMBINING DIAERESIS (Umlaut)
        switch (base) {
        case 'A':
            return 0x00C4;
        case 'E':
            return 0x00CB;
        case 'I':
            return 0x00CF;
        case 'O':
            return 0x00D6;
        case 'U':
            return 0x00DC;
        case 'Y':
            return 0x0178; // Ÿ (CP1252 0x9F)
        case 'a':
            return 0x00E4;
        case 'e':
            return 0x00EB;
        case 'i':
            return 0x00EF;
        case 'o':
            return 0x00F6;
        case 'u':
            return 0x00FC;
        case 'y':
            return 0x00FF;
        default:
            return 0;
        }
    case 0x030A: // COMBINING RING ABOVE
        switch (base) {
        case 'A':
            return 0x00C5;
        case 'a':
            return 0x00E5;
        case 'U':
        case 'u':
            return base;
        default:
            return 0;
        }
    case 0x0327: // COMBINING CEDILLA
        switch (base) {
        case 'C':
            return 0x00C7;
        case 'c':
            return 0x00E7;
        case 'S':
        case 's':
            return base;
        default:
            return 0;
        }
    case 0x030C: // COMBINING CARON
        switch (base) {
        case 'S':
            return 0x0160; // Š
        case 's':
            return 0x0161; // š
        case 'Z':
            return 0x017D; // Ž
        case 'z':
            return 0x017E; // ž
        case 'C':
        case 'c':
        case 'D':
        case 'd':
        case 'N':
        case 'n':
        case 'R':
        case 'r':
        case 'T':
        case 't':
            return base; // Latin transliteration
        default:
            return 0;
        }
    default:
        return 0;
    }
}

template <typename EmitFn> inline void decode_utf8_to_cp1252(std::string_view utf8, EmitFn&& emit) {
    std::size_t index = 0;
    while (index < utf8.size()) {
        uint32_t cp = decode_next_utf8_codepoint(utf8, index);

        // Fold any subsequent combining marks (NFD -> precomposed / transliterated)
        while (index < utf8.size()) {
            std::size_t peek_idx = index;
            const uint32_t next_cp = decode_next_utf8_codepoint(utf8, peek_idx);
            if (is_combining_mark(next_cp)) {
                index = peek_idx; // consume combining mark
                const uint32_t composed = compose_latin_pair(cp, next_cp);
                if (composed != 0) {
                    cp = composed;
                }
            } else {
                break;
            }
        }

        const auto mapped = map_codepoint_to_cp1252(cp);
        if (mapped.has_value()) {
            emit(*mapped);
        }
    }
}

inline void utf8_to_cp1252_append(std::string_view utf8, std::string& out) {
    if (is_pure_ascii(utf8)) {
        out.append(utf8);
        return;
    }
    out.reserve(out.size() + utf8.size());
    decode_utf8_to_cp1252(utf8, [&out](char c) { out.push_back(c); });
}

inline void append_pdf_escaped_text(std::string& dest, std::string_view utf8) {
    if (is_pure_ascii(utf8)) {
        for (char c : utf8) {
            append_pdf_char(dest, c);
        }
        return;
    }
    dest.reserve(dest.size() + utf8.size());
    decode_utf8_to_cp1252(utf8, [&dest](char c) { append_pdf_char(dest, c); });
}

} // namespace nordiska
