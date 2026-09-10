#include "nordiska/adapters/input/json_input_adapter.hpp"

#include <cstdint>
#include <fstream>
#include <limits>
#include <nlohmann/json.hpp>
#include <sstream>
#include <stdexcept>
#include <string>

namespace nordiska {
namespace {

using Json = nlohmann::json;

std::string read_text_file(const std::filesystem::path& path) {
    std::ifstream input(path, std::ios::binary);
    if (!input) {
        throw std::runtime_error("Could not open input file: " + path.string());
    }

    std::ostringstream contents;
    contents << input.rdbuf();
    if (input.bad()) {
        throw std::runtime_error("Could not read input file: " + path.string());
    }
    return contents.str();
}

const Json& required_member(const Json& object, const char* key, const std::string& context) {
    if (!object.contains(key)) {
        throw std::runtime_error("Missing JSON field: " + context + "." + key);
    }
    return object.at(key);
}

std::string required_string(const Json& object, const char* key, const std::string& context) {
    const Json& value = required_member(object, key, context);
    if (!value.is_string()) {
        throw std::runtime_error("JSON field must be a string: " + context + "." + key);
    }
    return value.get<std::string>();
}

std::int64_t required_integer(const Json& object, const char* key, const std::string& context) {
    const Json& value = required_member(object, key, context);
    if (!value.is_number_integer()) {
        throw std::runtime_error("JSON field must be a 64-bit integer: " + context + "." + key);
    }

    try {
        if (value.is_number_unsigned()) {
            const auto unsigned_value = value.get<std::uint64_t>();
            if (unsigned_value > static_cast<std::uint64_t>(std::numeric_limits<std::int64_t>::max())) {
                throw std::out_of_range("unsigned JSON integer does not fit int64");
            }
            return static_cast<std::int64_t>(unsigned_value);
        }
        return value.get<std::int64_t>();
    } catch (const Json::exception&) {
        throw std::runtime_error("JSON integer is outside the int64 range: " + context + "." + key);
    } catch (const std::out_of_range&) {
        throw std::runtime_error("JSON integer is outside the int64 range: " + context + "." + key);
    }
}

std::vector<std::string> optional_string_array(const Json& object, const char* key, const std::string& context) {
    if (!object.contains(key)) {
        return {};
    }
    const Json& values = object.at(key);
    if (!values.is_array()) {
        throw std::runtime_error("JSON field must be an array: " + context + "." + key);
    }
    std::vector<std::string> result;
    result.reserve(values.size());
    for (std::size_t index = 0; index < values.size(); ++index) {
        if (!values.at(index).is_string()) {
            throw std::runtime_error("JSON array value must be a string: " + context + "." + key + "[" +
                                     std::to_string(index) + "]");
        }
        result.push_back(values.at(index).get<std::string>());
    }
    return result;
}

Report parse_single_report(const Json& document, const std::string& context) {
    if (!document.is_object()) {
        throw std::runtime_error("Report JSON value must be an object: " + context);
    }
    Report report;
    report.account_number = required_string(document, "account_number", context);
    if (document.contains("title")) {
        report.title = required_string(document, "title", context);
    }
    report.summary_lines = optional_string_array(document, "summary_lines", context);

    const Json& transactions = required_member(document, "transactions", context);
    if (!transactions.is_array()) {
        throw std::runtime_error("JSON field must be an array: " + context + ".transactions");
    }

    report.transactions.reserve(transactions.size());
    for (std::size_t index = 0; index < transactions.size(); ++index) {
        const Json& transaction = transactions.at(index);
        const std::string tx_context = context + ".transactions[" + std::to_string(index) + "]";
        if (!transaction.is_object()) {
            throw std::runtime_error("JSON transaction must be an object: " + tx_context);
        }

        report.transactions.push_back(Transaction{required_string(transaction, "date", tx_context),
                                                  required_string(transaction, "type", tx_context),
                                                  required_string(transaction, "currency", tx_context),
                                                  required_integer(transaction, "amount_minor", tx_context)});
    }
    return report;
}

} // namespace

Report JsonInputAdapter::import(const std::filesystem::path& input_path) const {
    return import_text(read_text_file(input_path));
}

Report JsonInputAdapter::import_text(std::string_view contents) const {
    const auto reports = import_reports_text(contents);
    if (reports.empty()) {
        throw JsonInputError("No reports found in JSON");
    }
    return reports.front();
}

std::vector<Report> JsonInputAdapter::import_reports(const std::filesystem::path& input_path) const {
    return import_reports_text(read_text_file(input_path));
}

std::vector<Report> JsonInputAdapter::import_reports_text(std::string_view contents) const {
    try {
        const Json document = Json::parse(contents);
        if (document.is_array()) {
            if (document.empty()) {
                throw std::runtime_error("Report JSON array must not be empty");
            }
            std::vector<Report> reports;
            reports.reserve(document.size());
            for (std::size_t index = 0; index < document.size(); ++index) {
                reports.push_back(parse_single_report(document.at(index), "report[" + std::to_string(index) + "]"));
            }
            return reports;
        }
        if (document.is_object()) {
            return {parse_single_report(document, "report")};
        }
        throw std::runtime_error("Report JSON root must be an object or an array of objects");
    } catch (const Json::exception& error) {
        throw JsonInputError("Invalid JSON: " + std::string(error.what()));
    } catch (const std::runtime_error& error) {
        throw JsonInputError(error.what());
    }
}

} // namespace nordiska
