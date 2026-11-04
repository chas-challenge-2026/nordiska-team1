#pragma once

#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <span>
#include <string>
#include <string_view>

namespace nordiska::test {

// Cairo inserts a wall-clock CreationDate. Compare every other emitted byte so
// equivalent renderer outputs still match when generation crosses a second.
inline bool same_rendered_pdf(std::span<const uint8_t> lhs, std::span<const uint8_t> rhs) {
    const auto normalize = [](std::span<const uint8_t> bytes) {
        std::string pdf(reinterpret_cast<const char*>(bytes.data()), bytes.size());
        constexpr std::string_view marker = "/CreationDate (";
        const auto field = pdf.find(marker);
        if (field != std::string::npos) {
            const auto begin = field + marker.size();
            const auto end = pdf.find(')', begin);
            if (end != std::string::npos) {
                pdf.replace(begin, end - begin, "creation-date");
            }
        }
        return pdf;
    };
    return normalize(lhs) == normalize(rhs);
}

// In-memory structural validation of basic PDF layout
inline bool is_valid_pdf_structure(std::span<const uint8_t> bytes) {
    if (bytes.size() < 30) {
        return false;
    }
    std::string_view sv(reinterpret_cast<const char*>(bytes.data()), bytes.size());
    // 1. Header starts with %PDF-
    if (!sv.starts_with("%PDF-")) {
        return false;
    }
    // 2. Contains %%EOF near the end
    const auto eof_pos = sv.rfind("%%EOF");
    if (eof_pos == std::string_view::npos) {
        return false;
    }
    // 3. Contains startxref before %%EOF
    const auto startxref_pos = sv.rfind("startxref", eof_pos);
    if (startxref_pos == std::string_view::npos) {
        return false;
    }
    // 4. Contains trailer
    const auto trailer_pos = sv.rfind("trailer", startxref_pos);
    if (trailer_pos == std::string_view::npos) {
        return false;
    }
    // 5. Contains xref
    const auto xref_pos = sv.find("xref");
    if (xref_pos == std::string_view::npos) {
        return false;
    }
    // 6. Contains catalog and pages
    if (sv.find("/Catalog") == std::string_view::npos) {
        return false;
    }
    if (sv.find("/Pages") == std::string_view::npos) {
        return false;
    }
    return true;
}

// Full semantic and syntactic PDF validation using Ghostscript
inline bool validate_pdf_with_ghostscript(std::span<const uint8_t> bytes) {
    FILE* pipe = popen("gs -dNODISPLAY -dBATCH -dNOPAUSE -dPDFSTOPONERROR -dQUIET - 2>/dev/null", "w");
    if (!pipe) {
        return false;
    }
    size_t written = fwrite(bytes.data(), 1, bytes.size(), pipe);
    int status = pclose(pipe);
    return written == bytes.size() && status == 0;
}

} // namespace nordiska::test
