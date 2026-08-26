#pragma once

#include <cstdint>
#include <string>
#include <vector>

namespace nordiska {

struct Transaction {
    std::string date;
    std::string type;
    std::string currency;
    std::int64_t amount_minor{};
};

struct Report {
    std::string account_number;
    std::vector<Transaction> transactions;
};
} // namespace nordiska
