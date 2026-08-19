#include "nordiska/minimal_pdf_renderer.hpp"
#include <cstdint>
#include <fstream>
#include <iomanip>
#include <sstream>
#include <stdexcept>
#include <string>
#include <vector>
namespace nordiska {
namespace {
std::string format_minor_units(std::int64_t amount_minor, const std::string& currency) {
    const bool negative = amount_minor < 0;
    const std::uint64_t absolute = negative ? static_cast<std::uint64_t>(-(amount_minor + 1)) + 1
                                            : static_cast<std::uint64_t>(amount_minor);
    std::ostringstream formatted;
    if (negative) {
        formatted << '-';
    }
    formatted << absolute / 100 << '.' << std::setw(2) << std::setfill('0') << absolute % 100 << ' '
              << currency;
    return formatted.str();
}
std::string pdf_escape(const std::string& text) {
    std::string escaped;
    escaped.reserve(text.size());
    for (const char character : text) {
        if (character == '(' || character == ')' || character == '\\') {
            escaped.push_back('\\');
        }
        escaped.push_back(character);
    }
    return escaped;
}
std::string make_content_stream(const Report& report) {
    std::ostringstream content;
    content << "BT\n/F1 18 Tf\n72 760 Td\n"
            << '(' << pdf_escape("Nordiska transaction report") << ") Tj\n"
            << "0 -28 Td\n/F1 12 Tf\n"
            << '(' << pdf_escape("Account: " + report.account_number) << ") Tj\n"
            << "0 -24 Td\n"
            << '(' << pdf_escape("Transactions") << ") Tj\n";
    for (const Transaction& transaction : report.transactions) {
        content << "0 -18 Td\n("
                << pdf_escape(transaction.date + " | " + transaction.type + " | " +
                              format_minor_units(transaction.amount_minor, transaction.currency))
                << ") Tj\n";
    }
    content << "ET\n";
    return content.str();
}
} // namespace
void MinimalPdfRenderer::render(const Report& report, const std::filesystem::path& output_path) {
    const std::string content = make_content_stream(report);
    const std::vector<std::string> objects = {
        "<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] "
        "/Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
        "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        "<< /Length " + std::to_string(content.size()) + " >>\nstream\n" + content + "endstream"};
    std::ofstream output(output_path, std::ios::binary);
    if (!output) {
        throw std::runtime_error("Could not open output file: " + output_path.string());
    }
    output << "%PDF-1.4\n%\xFF\xFF\xFF\xFF\n";
    std::vector<std::streamoff> offsets;
    offsets.reserve(objects.size());
    for (std::size_t index = 0; index < objects.size(); ++index) {
        offsets.push_back(output.tellp());
        output << (index + 1) << " 0 obj\n" << objects[index] << "\nendobj\n";
    }
    const std::streamoff xref_offset = output.tellp();
    output << "xref\n0 " << (objects.size() + 1) << "\n"
           << "0000000000 65535 f \n";
    for (const std::streamoff offset : offsets) {
        output << std::setw(10) << std::setfill('0') << offset << " 00000 n \n";
    }
    output << "trailer\n<< /Size " << (objects.size() + 1) << " /Root 1 0 R >>\nstartxref\n"
           << xref_offset << "\n%%EOF\n";
}
} // namespace nordiska
