#include "nordiska/create_pdf.hpp"

#include <cctype>
#include <stdexcept>

namespace nordiska {

CreatePdf::CreatePdf(IPdfRenderer& renderer)
    : renderer_(renderer) {}

void CreatePdf::execute(const Report& report,
                        const std::filesystem::path& output_path) const {
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
                throw std::invalid_argument(
                    "currency must be an uppercase ISO 4217 code");
            }
        }
    }
    if (output_path.empty()) {
        throw std::invalid_argument("output_path must not be empty");
    }

    renderer_.render(report, output_path);
}

} // namespace nordiska
