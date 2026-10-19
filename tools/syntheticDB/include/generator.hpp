#pragma once

#include <string>
#include <string_view>
#include <vector>
#include <cstdint>
#include <span>
#include <chrono>

namespace nordiska::synthetic {

struct Customer {
    int64_t id;
    std::string personal_num;
    std::string name;
    std::string email;
    std::string phone_number;
    std::string password_hash;
    std::string created_at;
};

struct Account {
    int64_t id;
    int64_t customer_id;
    std::string account_number;
    std::string account_name;
    std::string account_type;
    double balance;
    double interest_rate;
    std::string status;
    std::string created_at;
};

struct LedgerEntry {
    int64_t id;
    int64_t account_id;
    std::string entry_type;
    double amount;
    std::string label;
    std::string created_at;
};

struct GeneratorConfig {
    int64_t customer_count{1000};
    int64_t start_customer_id{1};
    int64_t start_account_id{1};
    int64_t start_ledger_id{1};
    uint64_t master_seed{42};
    int year{2026};
    bool stress_text{false};
    int thread_count{16};
};

// Fixed ID ranges make account/ledger rows independent of worker scheduling.
// Max six accounts, each with <=445 ledger rows (400 activity draws plus
// monthly variation, opening deposit, interest and tax). Unused IDs are gaps.
inline constexpr int64_t account_id_stride = 6;
inline constexpr int64_t ledger_id_stride = 512;

// Fast 64-bit pseudo-random generator
class FastRng {
public:
    explicit FastRng(uint64_t seed);
    uint64_t next_u64();
    double next_double(); // [0.0, 1.0)
    int64_t randint(int64_t min_val, int64_t max_val);
    template <typename T>
    const T& choice(std::span<const T> items) {
        return items[static_cast<size_t>(randint(0, static_cast<int64_t>(items.size()) - 1))];
    }
private:
    uint64_t state_[4];
};

uint64_t stable_seed(uint64_t master_seed, int64_t index);
int luhn_checksum(std::string_view digits);
// Birth dates for ages 18..120 at the end of reporting year 2025.
inline constexpr auto personal_number_first_date = std::chrono::sys_days{std::chrono::year{1905}/1/1};
inline constexpr auto personal_number_end_date = std::chrono::sys_days{std::chrono::year{2008}/1/1};
inline constexpr uint64_t personal_number_date_count = (personal_number_end_date - personal_number_first_date).count();
inline constexpr uint64_t personal_number_capacity = personal_number_date_count * 1000;

// A seed chooses the starting position; ordinal must be below capacity.
// Pure, independent of thread scheduling, and requires no collision tracking.
std::string generate_personnummer(uint64_t seed, uint64_t ordinal);

// Compatibility for CLI append to older or differently seeded fixtures.
// A fixed-size bitmap skips existing identities; creation does not allocate it.
class PersonalNumberSequence {
public:
    explicit PersonalNumberSequence(uint64_t seed) : seed_(seed % personal_number_capacity) {}
    void reserve(std::string_view personal_number);
    uint64_t remaining() const { return personal_number_capacity - reserved_ - issued_; }
    std::string next();
private:
    uint64_t seed_, cursor_{0}, reserved_{0}, issued_{0};
    std::vector<uint64_t> occupied_;
};
int64_t pareto_sample(FastRng& rng, double x_min, double alpha, double x_max);
// Annual rate in basis points (350 = 3.5%). Changes affect the dated day's
// closing balance; posted monthly interest/tax compound from the next month.
int64_t monthly_interest_cents(int64_t opening_balance_cents,
                               std::span<const int64_t> daily_changes_cents,
                               int annual_rate_basis_points, int days_in_year);

// Internal generated labels/dates contain no COPY control characters.
void append_ledger_row(std::string& output, int64_t id, int64_t account_id,
                       std::string_view type, double amount, std::string_view label,
                       std::string_view date);

class StreamingBatchConsumer {
public:
    virtual ~StreamingBatchConsumer() = default;
    virtual void on_customers_chunk(std::string_view chunk, size_t row_count) = 0;
    virtual void on_accounts_chunk(std::string_view chunk, size_t row_count) = 0;
    virtual void on_ledger_chunk(std::string_view chunk, size_t row_count) = 0;
};

} // namespace nordiska::synthetic
