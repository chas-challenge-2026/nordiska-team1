#include "nordiska/diagnostics/benchmark_metrics.hpp"

#include <cmath>
#include <iostream>
#include <stdexcept>

int main() {
    const nordiska::benchmark::PhaseMetrics metrics{2.0, 10, 50, 1000};
    if (std::abs(metrics.reports_per_second() - 5.0) > 1e-9 ||
        std::abs(metrics.transactions_per_second() - 25.0) > 1e-9 || !metrics.output_bytes ||
        *metrics.output_bytes != 1000) {
        throw std::runtime_error("benchmark metric accounting is incorrect");
    }
    const nordiska::benchmark::PhaseMetrics zero{0.0, 10, 50, std::nullopt};
    if (zero.reports_per_second() != 0.0 || zero.transactions_per_second() != 0.0 ||
        zero.output_bytes) {
        throw std::runtime_error("zero-duration metric accounting is incorrect");
    }
    std::cout << "benchmark metric tests passed\n";
}
