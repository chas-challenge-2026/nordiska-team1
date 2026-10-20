#include "generator.hpp"

#include <array>
#include <charconv>
#include <cmath>
#include <string_view>
#include <algorithm>
#include <stdexcept>

namespace nordiska::synthetic {

static inline uint64_t rotl(const uint64_t x, int k) {
    return (x << k) | (x >> (64 - k));
}

FastRng::FastRng(uint64_t seed) {
    uint64_t z = seed + 0x9E3779B97F4A7C15ULL;
    for (int i = 0; i < 4; ++i) {
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9ULL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBULL;
        state_[i] = z ^ (z >> 31);
        z += 0x9E3779B97F4A7C15ULL;
    }
}

uint64_t FastRng::next_u64() {
    const uint64_t result = rotl(state_[1] * 5, 7) * 9;
    const uint64_t t = state_[1] << 17;
    state_[2] ^= state_[0];
    state_[3] ^= state_[1];
    state_[1] ^= state_[2];
    state_[0] ^= state_[3];
    state_[2] ^= t;
    state_[3] = rotl(state_[3], 45);
    return result;
}

double FastRng::next_double() {
    return (next_u64() >> 11) * (1.0 / 9007199254740992.0);
}

int64_t FastRng::randint(int64_t min_val, int64_t max_val) {
    if (min_val >= max_val) return min_val;
    uint64_t range = static_cast<uint64_t>(max_val - min_val + 1);
    return min_val + static_cast<int64_t>(next_u64() % range);
}

uint64_t stable_seed(uint64_t master_seed, int64_t index) {
    uint64_t h = master_seed ^ (static_cast<uint64_t>(index) * 0x9E3779B97F4A7C15ULL);
    h ^= h >> 33;
    h *= 0xFF51AFD7ED558CCDULL;
    h ^= h >> 33;
    h *= 0xC4CEB9FE1A85EC53ULL;
    h ^= h >> 33;
    return h;
}

int luhn_checksum(std::string_view digits) {
    int total = 0;
    for (size_t i = 0; i < digits.size(); ++i) {
        int d = digits[i] - '0';
        if (i % 2 == 0) {
            d *= 2;
        }
        if (d > 9) {
            d -= 9;
        }
        total += d;
    }
    return (10 - (total % 10)) % 10;
}

// A small immutable date table avoids calendar conversion in the hot path.
static constexpr auto PERSONAL_NUMBER_DATES = [] {
    std::array<std::array<char, 8>, personal_number_date_count> result{};
    size_t index = 0;
    const int first_year = int(std::chrono::year_month_day{personal_number_first_date}.year());
    const int end_year = int(std::chrono::year_month_day{personal_number_end_date}.year());
    for (int year = first_year; year < end_year; ++year) {
        for (unsigned month = 1; month <= 12; ++month) {
            const auto last = std::chrono::year_month_day_last{
                std::chrono::year{year}, std::chrono::month_day_last{std::chrono::month{month}}};
            for (unsigned day = 1; day <= unsigned(last.day()); ++day) {
                unsigned digits = year * 10000 + month * 100 + day;
                auto& date = result[index++];
                for (int digit = 7; digit >= 0; --digit) {
                    date[digit] = '0' + digits % 10;
                    digits /= 10;
                }
            }
        }
    }
    return result;
}();
static_assert(personal_number_capacity == 37'620'000);

std::string generate_personnummer(uint64_t seed, uint64_t ordinal) {
    if (ordinal >= personal_number_capacity)
        throw std::out_of_range("Personal-number capacity exhausted (37,620,000 customers)");
    const auto position = (seed % personal_number_capacity + ordinal) % personal_number_capacity;
    std::string result(12, '0');
    const auto& date = PERSONAL_NUMBER_DATES[position / 1000];
    std::copy(date.begin(), date.end(), result.begin());
    auto individual = position % 1000;
    for (int digit = 10; digit >= 8; --digit) {
        result[digit] = '0' + individual % 10;
        individual /= 10;
    }
    result[11] = '0' + luhn_checksum(std::string_view(result).substr(2, 9));
    return result;
}

void PersonalNumberSequence::reserve(std::string_view value) {
    if (cursor_ != 0) throw std::logic_error("Reserve existing identities before generating new ones");
    if (value.size() != 12 || !std::all_of(value.begin(), value.end(), [](char c) { return c >= '0' && c <= '9'; })) return;
    auto number = [&](size_t start, size_t count) {
        unsigned result = 0;
        for (size_t i = start; i < start + count; ++i) result = result * 10 + value[i] - '0';
        return result;
    };
    const auto date = std::chrono::year{int(number(0, 4))}/number(4, 2)/number(6, 2);
    if (!date.ok() || luhn_checksum(value.substr(2, 9)) != value[11] - '0') return;
    const auto days = std::chrono::sys_days{date};
    if (days < personal_number_first_date || days >= personal_number_end_date) return;
    const auto position = (days - personal_number_first_date).count() * 1000 + number(8, 3);
    if (occupied_.empty()) occupied_.resize((personal_number_capacity + 63) / 64);
    auto& word = occupied_[position / 64];
    const auto bit = uint64_t{1} << (position % 64);
    if (!(word & bit)) { word |= bit; ++reserved_; }
}

std::string PersonalNumberSequence::next() {
    if (remaining() == 0) throw std::out_of_range("Personal-number capacity exhausted (37,620,000 customers)");
    while (cursor_ < personal_number_capacity) {
        const auto ordinal = cursor_++;
        const auto position = (seed_ + ordinal) % personal_number_capacity;
        if (!occupied_.empty() && (occupied_[position / 64] & (uint64_t{1} << (position % 64)))) continue;
        ++issued_;
        return generate_personnummer(seed_, ordinal);
    }
    throw std::out_of_range("Personal-number capacity exhausted (37,620,000 customers)");
}

int64_t pareto_sample(FastRng& rng, double x_min, double alpha, double x_max) {
    double u = rng.next_double();
    if (u >= 0.999999) u = 0.999999;
    double val = x_min / std::pow(1.0 - u, 1.0 / alpha);
    double clamped = std::clamp(std::floor(val), x_min, x_max);
    return static_cast<int64_t>(clamped);
}

int64_t monthly_interest_cents(int64_t opening_balance_cents,
                               std::span<const int64_t> daily_changes_cents,
                               int annual_rate_basis_points, int days_in_year) {
    if (daily_changes_cents.empty() || daily_changes_cents.size() > 31 ||
        annual_rate_basis_points < 0 || (days_in_year != 365 && days_in_year != 366))
        throw std::invalid_argument("Invalid monthly interest period or annual rate");
    int64_t balance = opening_balance_cents;
    int64_t balance_days = 0;
    for (auto change : daily_changes_cents) {
        balance += change;
        balance_days += std::max<int64_t>(0, balance);
    }
    // Keep daily accrual unrounded; round only the month's payout to whole öre.
    const int64_t denominator = 10000LL * days_in_year;
    return (balance_days * annual_rate_basis_points + denominator / 2) / denominator;
}

void append_ledger_row(std::string& output, int64_t id, int64_t account_id,
                       std::string_view type, double amount, std::string_view label,
                       std::string_view date) {
    char buffer[128];
    auto integer = [&](int64_t value) {
        auto result = std::to_chars(buffer, buffer + sizeof(buffer), value);
        output.append(buffer, result.ptr); output += '\t';
    };
    integer(id); integer(account_id);
    output += type; output += '\t';
    auto result = std::to_chars(buffer, buffer + sizeof(buffer), amount, std::chars_format::fixed, 2);
    if (result.ec != std::errc{}) throw std::runtime_error("Ledger amount formatting failed");
    output.append(buffer, result.ptr); output += '\t'; output += label;
    output += "\t\\N\tf\t"; output += date; output += '\n';
}

} // namespace nordiska::synthetic
