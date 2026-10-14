#pragma once

#include <cstdint>
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

} // namespace nordiska::test
