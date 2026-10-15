#include "generator.hpp"

#include <array>
#include <iostream>
#include <stdexcept>

using namespace nordiska::synthetic;

static void expect(int64_t actual, int64_t expected) {
    if (actual != expected) throw std::runtime_error("Interest mismatch: expected " +
        std::to_string(expected) + " öre, got " + std::to_string(actual));
}

int main() {
    try {
        std::array<int64_t, 31> changes{};
        auto interest = [&](int64_t opening, int days, int year_days = 365, int rate = 350) {
            return monthly_interest_cents(opening, std::span(changes.data(), days), rate, year_days);
        };
        // 100,000 kr at 3.5% ANNUALLY, including both February lengths.
        expect(interest(10000000, 31), 29726);
        expect(interest(10000000, 30), 28767);
        expect(interest(10000000, 28), 26849);
        expect(interest(10000000, 29, 366), 27732);
        expect(interest(10000000, 31, 365, 0), 0);
        expect(interest(0, 31), 0);

        // Deposits earn interest from their dated day; they do not retroactively
        // earn a full month's interest just because they appear in closing balance.
        changes[30] = 10000000;
        expect(interest(0, 31), 959);
        changes = {};
        changes[15] = 10000000;
        expect(interest(0, 31), 15342); // January 16..31 inclusive.
        changes = {};
        changes[15] = -10000000;
        expect(interest(10000000, 31), 14384); // January 1..15 inclusive.
        changes = {};
        changes[0] = -10000000;
        expect(interest(10000000, 31), 0);

        // Accrue sub-öre daily amounts without daily rounding: 1 kr at 10% over
        // January earns one öre; rounding each day's interest would lose it all.
        changes = {};
        expect(interest(100, 31, 365, 1000), 1);

        // January's posted net payout becomes February's opening principal.
        const auto january = interest(1000000, 31);
        expect(january, 2973);
        const auto tax = (january * 30 + 50) / 100;
        expect(tax, 892);
        expect(interest(1000000 + january - tax, 28), 2691);
        std::cout << "PASS: annual-rate daily accrual, real month lengths, leap year, balance changes and net compounding\n";
    } catch (const std::exception& error) {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
