#include "nordiska/cairo_pdf_renderer.hpp"

#include <cairo/cairo-pdf.h>

#include <cstdint>
#include <exception>
#include <iomanip>
#include <span>
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

struct SinkWriter {
    IByteSink& sink;
    std::exception_ptr failure;
};

cairo_status_t write_to_sink(void* closure, const unsigned char* data, unsigned int length) {
    auto& writer = *static_cast<SinkWriter*>(closure);
    try {
        writer.sink.write(std::span<const std::byte>(reinterpret_cast<const std::byte*>(data), length));
        return CAIRO_STATUS_SUCCESS;
    } catch (...) {
        writer.failure = std::current_exception();
        return CAIRO_STATUS_WRITE_ERROR;
    }
}

void render_to_sink(const Report& report, IByteSink& sink) {
    SinkWriter writer{sink};
    cairo_surface_t* surface =
        cairo_pdf_surface_create_for_stream(write_to_sink, &writer, 612, 792);
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
        if (writer.failure != nullptr) {
            std::rethrow_exception(writer.failure);
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

void CairoPdfRenderer::render(const Report& report, IByteSink& sink) {
    render_to_sink(report, sink);
}

} // namespace nordiska
