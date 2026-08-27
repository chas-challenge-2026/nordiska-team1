#include "nordiska/delivery/c_api/document_c_api.h"

#include <cstdint>
#include <cstring>
#include <stdexcept>
#include <string>
#include <vector>

namespace {

void require(bool condition, const char* message) {
    if (!condition) {
        throw std::runtime_error(message);
    }
}

int save_pdf(const uint8_t* bytes, size_t length, size_t index, void* context) {
    auto& output = *static_cast<std::vector<uint8_t>*>(context);
    require(index == 0, "unexpected document index");
    output.assign(bytes, bytes + length);
    return 0;
}

int reject_pdf(const uint8_t*, size_t, size_t, void*) {
    return 1;
}

} // namespace

int main() {
    const std::string json =
        R"({"account_number":"SE123","title":"Nordiska tax report","transactions":[{"date":"2026-01-05","type":"deposit","currency":"SEK","amount_minor":100000}],"summary_lines":["Capital tax (30%): 30.00 SEK"]})";
    char error[256]{};
    std::vector<uint8_t> pdf;

    const int status =
        nordiska_document_generate_json(reinterpret_cast<const uint8_t*>(json.data()), json.size(),
                                        save_pdf, &pdf, error, sizeof(error));
    require(status == NORDISKA_DOCUMENT_OK, error);
    require(pdf.size() > 8, "PDF callback did not receive document bytes");
    require(std::memcmp(pdf.data(), "%PDF-", 5) == 0, "output is not a PDF");
    require(std::memcmp(pdf.data(), "%PDF-1.3", 8) == 0,
            "C API did not use the Haru production composition");

    const std::string invalid_json = "{}";
    const int invalid_status =
        nordiska_document_generate_json(reinterpret_cast<const uint8_t*>(invalid_json.data()),
                                        invalid_json.size(), save_pdf, &pdf, error, sizeof(error));
    require(invalid_status == NORDISKA_DOCUMENT_INVALID_INPUT,
            "invalid report did not return invalid-input status");
    require(std::strlen(error) > 0, "invalid report did not return an error message");

    const std::string invalid_report =
        R"({"account_number":"","transactions":[{"date":"2026-01-05","type":"deposit","currency":"SEK","amount_minor":1}]})";
    const int invalid_report_status = nordiska_document_generate_json(
        reinterpret_cast<const uint8_t*>(invalid_report.data()), invalid_report.size(), save_pdf,
        &pdf, error, sizeof(error));
    require(invalid_report_status == NORDISKA_DOCUMENT_INVALID_INPUT,
            "invalid domain report did not return invalid-input status");

    const int callback_status =
        nordiska_document_generate_json(reinterpret_cast<const uint8_t*>(json.data()), json.size(),
                                        reject_pdf, nullptr, error, sizeof(error));
    require(callback_status == NORDISKA_DOCUMENT_CALLBACK_FAILED,
            "callback failure did not return callback-failed status");
}
