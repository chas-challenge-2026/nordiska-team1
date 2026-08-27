#pragma once

#include <cstddef>
#include <optional>

namespace nordiska::benchmark {

struct PhaseMetrics {
    double seconds{};
    std::size_t reports{};
    std::size_t transactions{};
    std::optional<std::size_t> output_bytes;

    double reports_per_second() const noexcept;
    double transactions_per_second() const noexcept;
};

} // namespace nordiska::benchmark
