#include "pdf_engine_impl.hpp"

#include <cairo-pdf.h>
#include <cairo.h>
#include <cstdint>
#include <memory>
#include <string>
#include <vector>

namespace nordiska {

namespace {

struct CairoDeleter {
    void operator()(cairo_t* cr) const noexcept {
        if (cr != nullptr) {
            cairo_destroy(cr);
        }
    }
};
using UniqueCairo = std::unique_ptr<cairo_t, CairoDeleter>;

struct CairoSurfaceDeleter {
    void operator()(cairo_surface_t* surface) const noexcept {
        if (surface != nullptr) {
            cairo_surface_destroy(surface);
        }
    }
};
using UniqueCairoSurface = std::unique_ptr<cairo_surface_t, CairoSurfaceDeleter>;

struct CairoFontFaceDeleter {
    void operator()(cairo_font_face_t* face) const noexcept {
        if (face != nullptr) {
            cairo_font_face_destroy(face);
        }
    }
};
using UniqueCairoFontFace = std::unique_ptr<cairo_font_face_t, CairoFontFaceDeleter>;

class SharedCairoFonts {
  public:
    static const SharedCairoFonts& instance() {
        static SharedCairoFonts inst;
        return inst;
    }

    [[nodiscard]] cairo_font_face_t* normal() const noexcept {
        return font_normal_.get();
    }
    [[nodiscard]] cairo_font_face_t* bold() const noexcept {
        return font_bold_.get();
    }

  private:
    SharedCairoFonts()
        : font_normal_(cairo_toy_font_face_create("Helvetica", CAIRO_FONT_SLANT_NORMAL, CAIRO_FONT_WEIGHT_NORMAL)),
          font_bold_(cairo_toy_font_face_create("Helvetica", CAIRO_FONT_SLANT_NORMAL, CAIRO_FONT_WEIGHT_BOLD)) {}

    UniqueCairoFontFace font_normal_;
    UniqueCairoFontFace font_bold_;
};

class CairoEngineImpl final : public PdfEngine::Impl {
  public:
    CairoEngineImpl() {
        // Eagerly ensure shared fonts are front-loaded at startup
        (void)SharedCairoFonts::instance();
    }

    [[nodiscard]] std::expected<std::vector<uint8_t>, RenderError> render(const DocumentLayout& layout) const override {
        std::vector<uint8_t> buffer;

        auto write_callback = [](void* closure, const unsigned char* data, unsigned int length) -> cairo_status_t {
            auto* out = static_cast<std::vector<uint8_t>*>(closure);
            out->insert(out->end(), data, data + length);
            return CAIRO_STATUS_SUCCESS;
        };

        const float default_w = layout.pages.empty() ? 612.0F : layout.pages.front().width;
        const float default_h = layout.pages.empty() ? 792.0F : layout.pages.front().height;

        UniqueCairoSurface surface(cairo_pdf_surface_create_for_stream(write_callback, &buffer, default_w, default_h));
        if (!surface || cairo_surface_status(surface.get()) != CAIRO_STATUS_SUCCESS) {
            return std::unexpected(RenderError{
                .kind = RenderErrorKind::EngineError,
                .message = "cairo: failed to create PDF surface",
            });
        }

        UniqueCairo cr(cairo_create(surface.get()));
        if (!cr || cairo_status(cr.get()) != CAIRO_STATUS_SUCCESS) {
            return std::unexpected(RenderError{
                .kind = RenderErrorKind::EngineError,
                .message = "cairo: failed to create drawing context",
            });
        }

        cairo_set_source_rgb(cr.get(), 0.0, 0.0, 0.0);

        const auto& fonts = SharedCairoFonts::instance();

        for (const PageLayout& page_layout : layout.pages) {
            cairo_pdf_surface_set_size(surface.get(), page_layout.width, page_layout.height);

            // Draw Lines with line width state caching
            float current_line_width = -1.0F;
            for (const PositionedLine& line : page_layout.lines) {
                if (line.line_width != current_line_width) {
                    cairo_set_line_width(cr.get(), line.line_width);
                    current_line_width = line.line_width;
                }
                cairo_move_to(cr.get(), line.x1, line.y1);
                cairo_line_to(cr.get(), line.x2, line.y2);
                cairo_stroke(cr.get());
            }

            // Draw Texts with font face and size state caching (front-loaded fonts)
            cairo_font_face_t* current_font_face = nullptr;
            float current_font_size = -1.0F;

            for (const PositionedText& text : page_layout.texts) {
                cairo_font_face_t* target_face = (text.weight == FontWeight::Bold) ? fonts.bold() : fonts.normal();
                if (target_face != current_font_face) {
                    cairo_set_font_face(cr.get(), target_face);
                    current_font_face = target_face;
                }
                if (text.font_size != current_font_size) {
                    cairo_set_font_size(cr.get(), text.font_size);
                    current_font_size = text.font_size;
                }
                cairo_move_to(cr.get(), text.x, text.y);
                cairo_show_text(cr.get(), text.text.c_str());
            }

            cairo_show_page(cr.get());
        }

        cr.reset();
        cairo_surface_finish(surface.get());
        const cairo_status_t status = cairo_surface_status(surface.get());
        surface.reset();

        if (status != CAIRO_STATUS_SUCCESS) {
            return std::unexpected(RenderError{
                .kind = RenderErrorKind::EngineError,
                .message = std::string("cairo: rendering failed: ") + cairo_status_to_string(status),
            });
        }

        return buffer;
    }
};

} // namespace

std::unique_ptr<PdfEngine::Impl> make_cairo_engine() {
    return std::make_unique<CairoEngineImpl>();
}

} // namespace nordiska
