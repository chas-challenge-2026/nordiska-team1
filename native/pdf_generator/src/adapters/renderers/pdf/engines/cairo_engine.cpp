#include "../pdf_engine.hpp"

#include <cairo/cairo-pdf.h>
#include <exception>
#include <memory>
#include <span>
#include <stdexcept>
#include <string>

namespace nordiska::pdf::detail {
namespace {

void check(cairo_t* context, const char* operation) {
    const cairo_status_t status = cairo_status(context);
    if (status != CAIRO_STATUS_SUCCESS) {
        throw std::runtime_error(std::string("Cairo ") + operation + " failed: " + cairo_status_to_string(status));
    }
}

struct CairoDeleter {
    void operator()(cairo_t* context) const noexcept {
        if (context != nullptr) {
            cairo_destroy(context);
        }
    }
};

struct CairoSurfaceDeleter {
    void operator()(cairo_surface_t* surface) const noexcept {
        if (surface != nullptr) {
            cairo_surface_destroy(surface);
        }
    }
};

using UniqueCairo = std::unique_ptr<cairo_t, CairoDeleter>;
using UniqueCairoSurface = std::unique_ptr<cairo_surface_t, CairoSurfaceDeleter>;

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

class CairoEngine final : public IPdfEngine {
  public:
    void render(const Document& document, IByteSink& sink) override {
        SinkWriter writer{sink};
        UniqueCairoSurface surface(cairo_pdf_surface_create_for_stream(write_to_sink, &writer, 612, 792));
        if (!surface || cairo_surface_status(surface.get()) != CAIRO_STATUS_SUCCESS) {
            const auto status = !surface ? CAIRO_STATUS_NO_MEMORY : cairo_surface_status(surface.get());
            throw std::runtime_error(std::string("Cairo PDF surface failed: ") + cairo_status_to_string(status));
        }

        UniqueCairo context(cairo_create(surface.get()));
        check(context.get(), "create context");
        cairo_select_font_face(context.get(), "Helvetica", CAIRO_FONT_SLANT_NORMAL, CAIRO_FONT_WEIGHT_NORMAL);
        cairo_set_source_rgb(context.get(), 0, 0, 0);

        for (const Page& page : document.pages) {
            double y = 52;
            for (const TextLine& line : page.lines) {
                cairo_set_font_size(context.get(), line.style == TextStyle::title ? 18 : 12);
                cairo_move_to(context.get(), 72, y);
                cairo_show_text(context.get(), line.text.c_str());
                check(context.get(), "write text");
                y += line.style == TextStyle::title ? 30 : 18;
            }
            cairo_show_page(context.get());
            check(context.get(), "finish page");
        }

        context.reset();
        cairo_surface_finish(surface.get());
        const cairo_status_t status = cairo_surface_status(surface.get());
        surface.reset();
        if (status != CAIRO_STATUS_SUCCESS) {
            throw std::runtime_error(std::string("Cairo PDF output failed: ") + cairo_status_to_string(status));
        }
        if (writer.failure != nullptr) {
            std::rethrow_exception(writer.failure);
        }
    }
};

} // namespace

std::unique_ptr<IPdfEngine> make_cairo_engine() {
    return std::make_unique<CairoEngine>();
}

} // namespace nordiska::pdf::detail
