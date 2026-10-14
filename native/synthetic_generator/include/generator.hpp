#pragma once

#include <string>
#include <string_view>
#include <vector>
#include <cstdint>
#include <random>
#include <span>
#include <memory>
#include <functional>

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
std::string generate_personnummer(FastRng& rng);
int64_t pareto_sample(FastRng& rng, double x_min, double alpha, double x_max);

class StreamingBatchConsumer {
public:
    virtual ~StreamingBatchConsumer() = default;
    virtual void on_customers_chunk(std::string_view chunk, size_t row_count) = 0;
    virtual void on_accounts_chunk(std::string_view chunk, size_t row_count) = 0;
    virtual void on_ledger_chunk(std::string_view chunk, size_t row_count) = 0;
};

} // namespace nordiska::synthetic
