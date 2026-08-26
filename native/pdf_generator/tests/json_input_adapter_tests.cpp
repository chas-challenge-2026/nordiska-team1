#include "nordiska/json_input_adapter.hpp"

#include <filesystem>
#include <fstream>
#include <iostream>
#include <stdexcept>
#include <string>

namespace {

void require(bool condition, const std::string& message) {
    if (!condition) {
        throw std::runtime_error(message);
    }
}

void write_file(const std::filesystem::path& path, const std::string& contents) {
    std::ofstream output(path);
    require(static_cast<bool>(output), "could not create test input");
    output << contents;
}

void require_failure(const std::filesystem::path& path, const std::string& expected_text) {
    try {
        const nordiska::JsonInputAdapter json_input_adapter;
        const nordiska::IInputAdapter& input_adapter = json_input_adapter;
        (void)input_adapter.import(path);
    } catch (const std::runtime_error& error) {
        require(std::string(error.what()).find(expected_text) != std::string::npos,
                "unexpected error: " + std::string(error.what()));
        return;
    }
    throw std::runtime_error("invalid JSON input was accepted");
}

void valid_report_is_parsed_without_regex_ambiguity(const std::filesystem::path& directory) {
    const auto path = directory / "valid.json";
    write_file(path, R"({
        "account_number": "SE\"123",
        "transactions": [
            {
                "date": "2026-01-05",
                "type": "deposit",
                "currency": "SEK",
                "amount_minor": 100000,
                "metadata": {"amount_minor": "not a number"}
            },
            {
                "date": "2026-01-06",
                "type": "withdrawal",
                "currency": "SEK",
                "amount_minor": -2500
            }
        ]
    })");

    const nordiska::JsonInputAdapter json_input_adapter;
    const nordiska::IInputAdapter& input_adapter = json_input_adapter;
    const nordiska::Report report = input_adapter.import(path);
    require(report.account_number == "SE\"123", "escaped account number was not decoded");
    require(report.transactions.size() == 2, "wrong transaction count");
    require(report.transactions[0].amount_minor == 100000, "wrong first amount");
    require(report.transactions[1].amount_minor == -2500, "wrong second amount");
}

void malformed_json_is_rejected(const std::filesystem::path& directory) {
    const auto path = directory / "malformed.json";
    write_file(path, R"({"account_number":"A","transactions":[)");
    require_failure(path, "Invalid JSON");
}

void wrong_types_and_missing_fields_are_rejected(const std::filesystem::path& directory) {
    const auto missing_path = directory / "missing.json";
    write_file(missing_path, R"({"account_number":"A"})");
    require_failure(missing_path, "report.transactions");

    const auto type_path = directory / "wrong-type.json";
    write_file(type_path, R"({"account_number":"A","transactions":{}})");
    require_failure(type_path, "report.transactions");

    const auto transaction_path = directory / "wrong-transaction.json";
    write_file(transaction_path,
               R"({
            "account_number": "A",
            "transactions": [
                {
                    "date": "2026-01-05",
                    "type": "deposit",
                    "currency": "SEK",
                    "amount_minor": 1.5
                }
            ]
        })");
    require_failure(transaction_path, "amount_minor");
}

void integers_outside_int64_are_rejected(const std::filesystem::path& directory) {
    const auto path = directory / "large-integer.json";
    write_file(path,
               R"({
            "account_number": "A",
            "transactions": [
                {
                    "date": "2026-01-05",
                    "type": "deposit",
                    "currency": "SEK",
                    "amount_minor": 9223372036854775808
                }
            ]
        })");
    require_failure(path, "int64 range");
}

} // namespace

int main() {
    const auto directory =
        std::filesystem::temp_directory_path() / "nordiska-json-report-reader-tests";
    std::filesystem::remove_all(directory);
    std::filesystem::create_directories(directory);

    try {
        valid_report_is_parsed_without_regex_ambiguity(directory);
        malformed_json_is_rejected(directory);
        wrong_types_and_missing_fields_are_rejected(directory);
        integers_outside_int64_are_rejected(directory);
    } catch (...) {
        std::filesystem::remove_all(directory);
        throw;
    }

    std::filesystem::remove_all(directory);
    std::cout << "JSON report reader tests passed\n";
}
