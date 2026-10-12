#pragma once

#include <cstdint>
#include <string>
#include <vector>

namespace nordiska {

struct StatementTransaction {
    std::string date;
    std::string type;
    std::string description;
    std::string currency;
    std::int64_t amount_minor{0};
    std::string amount_display;
    std::string balance_after_display;
};

struct AccountStatement {
    std::string title;
    std::string account_number;
    std::string account_name;
    std::string currency;
    std::string period;
    std::string opening_balance;
    std::string closing_balance;
    std::vector<StatementTransaction> transactions;
};

struct AnnualTaxReport {
    std::string title;
    std::string tax_year;
    std::string account_number;
    std::string account_name;
    std::string total_interest_earned;
    std::string preliminary_tax_deducted;
    std::string reported_to_authority;
};

} // namespace nordiska
