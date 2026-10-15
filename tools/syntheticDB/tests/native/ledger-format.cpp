#include "generator.hpp"
#include <cassert>
#include <format>

using namespace nordiska::synthetic;
int main() {
    FastRng rng(42);
    std::string actual;
    // Preserve decimal rounding, negative zero, UTF-8 labels and COPY nulls.
    for (int i = 0; i < 100000; ++i) {
        const auto amount = i == 0 ? -0.0 : (rng.next_double() - 0.5) * 1000000;
        actual.clear();
        append_ledger_row(actual, 115568639999LL, 225719999, "tax", amount, "Preliminärskatt 30% åäö", "2025-02-28 23:59:55+00");
        assert(actual == std::format("115568639999\t225719999\ttax\t{:.2f}\tPreliminärskatt 30% åäö\t\\N\tf\t2025-02-28 23:59:55+00\n", amount));
    }
}
