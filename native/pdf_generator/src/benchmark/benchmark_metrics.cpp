#include "nordiska/benchmark_metrics.hpp"

namespace nordiska::benchmark {

double PhaseMetrics::reports_per_second() const noexcept {
    return seconds > 0.0 ? static_cast<double>(reports) / seconds : 0.0;
}

double PhaseMetrics::transactions_per_second() const noexcept {
    return seconds > 0.0 ? static_cast<double>(transactions) / seconds : 0.0;
}

} // namespace nordiska::benchmark
