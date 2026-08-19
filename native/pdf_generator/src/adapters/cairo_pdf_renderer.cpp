#include "nordiska/cairo_pdf_renderer.hpp"

#include "atomic_output.hpp"

#include <cairo/cairo-pdf.h>

#include <cstdint>
#include <iomanip>
#include <sstream>
#include <stdexcept>
#include <string>

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

std::string transaction_line(const Transaction& transaction) {
    return transaction.date + " | " + transaction.type + " | " +
           format_minor_units(transaction.amount_minor, transaction.currency);
}

void check(cairo_t* context, const char* operation) {
    const cairo_status_t status = cairo_status(context);
    if (status != CAIRO_STATUS_SUCCESS) {
        throw std::runtime_error(std::string("Cairo ") + operation +
                                 " failed: " + cairo_status_to_string(status));
    }
}

void render_to_file(const Report& report, const std::filesystem::path& path) {
    cairo_surface_t* surface = cairo_pdf_surface_create(path.c_str(), 612, 792);
    if (surface == nullptr || cairo_surface_status(surface) != CAIRO_STATUS_SUCCESS) {
        const auto status =
            surface == nullptr ? CAIRO_STATUS_NO_MEMORY : cairo_surface_status(surface);
        if (surface != nullptr) {
            cairo_surface_destroy(surface);
        }
        throw std::runtime_error(std::string("Cairo PDF surface failed: ") +
                                 cairo_status_to_string(status));
    }

    cairo_t* context = cairo_create(surface);
    try {
        check(context, "create context");
        cairo_select_font_face(context, "Helvetica", CAIRO_FONT_SLANT_NORMAL,
                               CAIRO_FONT_WEIGHT_NORMAL);
        cairo_set_source_rgb(context, 0, 0, 0);
        cairo_set_font_size(context, 18);
        cairo_move_to(context, 72, 52);
        cairo_show_text(context, "Nordiska transaction report");
        check(context, "write title");

        cairo_set_font_size(context, 12);
        double y = 82;
        const auto write_line = [&](const std::string& line) {
            if (y > 750) {
                cairo_show_page(context);
                check(context, "start page");
                y = 52;
            }
            cairo_move_to(context, 72, y);
            cairo_show_text(context, line.c_str());
            check(context, "write text");
            y += 18;
        };

        write_line("Account: " + report.account_number);
        write_line("Transactions");
        for (const Transaction& transaction : report.transactions) {
            write_line(transaction_line(transaction));
        }
        cairo_show_page(context);
        check(context, "finish page");
        cairo_destroy(context);
        context = nullptr;
        cairo_surface_finish(surface);
        const cairo_status_t status = cairo_surface_status(surface);
        cairo_surface_destroy(surface);
        if (status != CAIRO_STATUS_SUCCESS) {
            throw std::runtime_error(std::string("Cairo PDF output failed: ") +
                                     cairo_status_to_string(status));
        }
    } catch (...) {
        if (context != nullptr) {
            cairo_destroy(context);
        }
        cairo_surface_destroy(surface);
        throw;
    }
}

} // namespace

void CairoPdfRenderer::render(const Report& report, const std::filesystem::path& output_path) {
    detail::write_atomically(output_path, [&](const std::filesystem::path& temporary_path) {
        render_to_file(report, temporary_path);
    });
}

} // namespace nordiska
