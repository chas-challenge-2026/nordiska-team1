#include "nordiska/libharu_pdf_renderer.hpp"

#include <cstdint>
#include <hpdf.h>
#include <iomanip>
#include <span>
#include <sstream>
#include <stdexcept>
#include <string>
#include <vector>

namespace nordiska {
namespace {

struct HaruError {
    HPDF_STATUS code{};
    HPDF_STATUS detail{};
};

void error_handler(HPDF_STATUS error_no, HPDF_STATUS detail_no, void* user_data) {
    auto* error = static_cast<HaruError*>(user_data);
    error->code = error_no;
    error->detail = detail_no;
}

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

std::string transaction_line(const Transaction& transaction) {
    return transaction.date + " | " + transaction.type + " | " +
           format_minor_units(transaction.amount_minor, transaction.currency);
}

void check(HaruError& error, HPDF_STATUS status, const char* operation) {
    if (status != HPDF_OK) {
        std::ostringstream message;
        message << "libharu " << operation << " failed (" << status << ", " << error.detail << ')';
        throw std::runtime_error(message.str());
    }
}

void render_to_sink(const Report& report, IByteSink& sink) {
    HaruError error;
    HPDF_Doc pdf = HPDF_New(error_handler, &error);
    if (pdf == nullptr) {
        throw std::runtime_error("libharu could not create a document");
    }

    try {
        check(error, HPDF_SetCompressionMode(pdf, HPDF_COMP_ALL), "set compression");
        HPDF_Page page = HPDF_AddPage(pdf);
        if (page == nullptr) {
            throw std::runtime_error("libharu add page failed");
        }
        check(error, HPDF_Page_SetWidth(page, 612), "set page width");
        check(error, HPDF_Page_SetHeight(page, 792), "set page height");

        HPDF_Font font = HPDF_GetFont(pdf, "Helvetica", nullptr);
        if (font == nullptr) {
            throw std::runtime_error("libharu get font failed");
        }
        check(error, HPDF_Page_SetFontAndSize(page, font, 18), "set title font");
        check(error, HPDF_Page_BeginText(page), "begin text");
        check(error, HPDF_Page_TextOut(page, 72, 740, report.title.c_str()), "write title");
        check(error, HPDF_Page_SetFontAndSize(page, font, 12), "set body font");

        float y = 710;
        const auto write_line = [&](const std::string& line) {
            if (y < 54) {
                check(error, HPDF_Page_EndText(page), "end text");
                page = HPDF_AddPage(pdf);
                if (page == nullptr) {
                    throw std::runtime_error("libharu add page failed");
                }
                check(error, HPDF_Page_SetFontAndSize(page, font, 12), "set page font");
                check(error, HPDF_Page_BeginText(page), "begin page text");
                y = 740;
            }
            check(error, HPDF_Page_TextOut(page, 72, y, line.c_str()), "write text");
            y -= 18;
        };

        write_line("Account: " + report.account_number);
        write_line("Transactions");
        for (const Transaction& transaction : report.transactions) {
            write_line(transaction_line(transaction));
        }
        for (const std::string& line : report.summary_lines) {
            write_line(line);
        }
        check(error, HPDF_Page_EndText(page), "end text");
        check(error, HPDF_SaveToStream(pdf), "save document to stream");
        std::vector<std::byte> buffer(64 * 1024);
        while (true) {
            HPDF_UINT32 size = static_cast<HPDF_UINT32>(buffer.size());
            const HPDF_STATUS status =
                HPDF_ReadFromStream(pdf, reinterpret_cast<HPDF_BYTE*>(buffer.data()), &size);
            if (status != HPDF_OK && status != HPDF_STREAM_EOF) {
                check(error, status, "read document stream");
            }
            if (size != 0) {
                sink.write(std::span<const std::byte>(buffer.data(), size));
            }
            if (status == HPDF_STREAM_EOF || size == 0) {
                break;
            }
        }
        HPDF_Free(pdf);
    } catch (...) {
        HPDF_Free(pdf);
        throw;
    }
}

} // namespace

void LibHaruPdfRenderer::render(const Report& report, IByteSink& sink) {
    render_to_sink(report, sink);
}

} // namespace nordiska
