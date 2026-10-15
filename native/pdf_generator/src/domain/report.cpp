#include "nordiska/domain/report.hpp"

#include <cctype>
#include <stdexcept>

namespace nordiska {

void validate_report(const Report& report) {
    if (report.account_number.empty()) {
        throw std::invalid_argument("account_number must not be empty");
    }
    if (report.transactions.empty()) {
        throw std::invalid_argument("transactions must not be empty");
    }
    for (const Transaction& transaction : report.transactions) {
        if (transaction.currency.size() != 3) {
            throw std::invalid_argument("currency must be a three-letter ISO 4217 code");
        }
        for (const unsigned char character : transaction.currency) {
            if (!std::isupper(character)) {
                throw std::invalid_argument("currency must be an uppercase ISO 4217 code");
            }
        }
    }
}

} // namespace nordiska
