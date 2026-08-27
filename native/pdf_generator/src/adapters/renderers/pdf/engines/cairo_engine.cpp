#include "../pdf_engine.hpp"

#include <cairo/cairo-pdf.h>
#include <exception>
#include <span>
#include <stdexcept>
#include <string>

namespace nordiska::pdf::detail {
namespace {

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
        writer.sink.write(
            std::span<const std::byte>(reinterpret_cast<const std::byte*>(data), length));
        return CAIRO_STATUS_SUCCESS;
    } catch (...) {
        writer.failure = std::current_exception();
        return CAIRO_STATUS_WRITE_ERROR;
    }
}

class CairoEngine final : public IPdfEngine {
  public:
    void render(const Document& document, IByteSink& sink) override {
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

            for (const Page& page : document.pages) {
                double y = 52;
                for (const TextLine& line : page.lines) {
                    cairo_set_font_size(context, line.style == TextStyle::title ? 18 : 12);
                    cairo_move_to(context, 72, y);
                    cairo_show_text(context, line.text.c_str());
                    check(context, "write text");
                    y += line.style == TextStyle::title ? 30 : 18;
                }
                cairo_show_page(context);
                check(context, "finish page");
            }

            cairo_destroy(context);
            context = nullptr;
            cairo_surface_finish(surface);
            const cairo_status_t status = cairo_surface_status(surface);
            cairo_surface_destroy(surface);
            surface = nullptr;
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
            if (surface != nullptr) {
                cairo_surface_destroy(surface);
            }
            throw;
        }
    }
};

} // namespace

std::unique_ptr<IPdfEngine> make_cairo_engine() {
    return std::make_unique<CairoEngine>();
}

} // namespace nordiska::pdf::detail
