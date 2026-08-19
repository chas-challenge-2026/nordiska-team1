#include "nordiska/json_report_reader.hpp"

#include <fstream>
#include <regex>
#include <sstream>
#include <stdexcept>

namespace nordiska {
namespace {

std::string read_text_file(const std::filesystem::path& path) {
    std::ifstream input(path, std::ios::binary);
    if (!input) {
        throw std::runtime_error("Could not open input file: " + path.string());
    }

    std::ostringstream contents;
    contents << input.rdbuf();
    return contents.str();
}

std::string required_string(const std::string& object, const std::string& key) {
    const std::regex pattern("\"" + key + "\"\\s*:\\s*\"([^\"]*)\"");
    std::smatch match;
    if (!std::regex_search(object, match, pattern)) {
        throw std::runtime_error("Missing JSON string field: " + key);
    }
    return match[1].str();
}

std::int64_t required_integer(const std::string& object, const std::string& key) {
    const std::regex pattern("\"" + key + "\"\\s*:\\s*(-?[0-9]+)");
    std::smatch match;
    if (!std::regex_search(object, match, pattern)) {
        throw std::runtime_error("Missing JSON integer field: " + key);
    }
    return std::stoll(match[1].str());
}

} // namespace

Report read_report_json(const std::filesystem::path& input_path) {
    // Temporary adapter for the first skeleton. Once the canonical schema is
    // agreed, replace this implementation with a real JSON library here.
    const std::string json = read_text_file(input_path);
    Report report;
    report.account_number = required_string(json, "account_number");

    const std::regex object_pattern(R"(\{[^{}]*\})");
    for (std::sregex_iterator it(json.begin(), json.end(), object_pattern), end;
         it != end;
         ++it) {
        const std::string object = it->str();
        if (object.find("\"date\"") == std::string::npos ||
            object.find("\"type\"") == std::string::npos ||
            object.find("\"currency\"") == std::string::npos ||
            object.find("\"amount_minor\"") == std::string::npos) {
            continue;
        }

        report.transactions.push_back(Transaction{
            required_string(object, "date"),
            required_string(object, "type"),
            required_string(object, "currency"),
            required_integer(object, "amount_minor")});
    }

    return report;
}

} // namespace nordiska
