#include "nordiska/rendering/cairo_pdf_engine.hpp"

#include "nordiska/text/text_metrics.hpp"

#include <algorithm>
#include <array>
#include <cairo-pdf.h>
#include <cairo.h>
#include <cstdint>
#include <cstdlib>
#include <cstring>
#include <memory>
#include <string>
#include <type_traits>
#include <unordered_map>
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

static UniqueCairoFontFace create_font_face_with_fallback(cairo_font_weight_t weight) {
    if (const char* override_font = std::getenv("NORDISKA_PDF_CAIRO_FONT_OVERRIDE");
        override_font != nullptr && std::strcmp(override_font, "__FORCE_FAILURE__") == 0) {
        return nullptr;
    }

    constexpr std::array<const char*, 3> kFontCandidates = {"Helvetica", "Arial", "sans-serif"};
    for (const char* family : kFontCandidates) {
        cairo_font_face_t* face = cairo_toy_font_face_create(family, CAIRO_FONT_SLANT_NORMAL, weight);
        if (face != nullptr) {
            const cairo_status_t status = cairo_font_face_status(face);
            if (status == CAIRO_STATUS_SUCCESS) {
                return UniqueCairoFontFace(face);
            }
            cairo_font_face_destroy(face);
        }
    }
    return nullptr;
}

class SharedCairoFonts {
  public:
    static const SharedCairoFonts& instance(bool force_reload = false) {
        static SharedCairoFonts inst;
        if (force_reload) {
            inst.reload();
        }
        return inst;
    }

    [[nodiscard]] bool is_valid() const noexcept {
        return font_normal_ != nullptr && cairo_font_face_status(font_normal_.get()) == CAIRO_STATUS_SUCCESS &&
               font_bold_ != nullptr && cairo_font_face_status(font_bold_.get()) == CAIRO_STATUS_SUCCESS;
    }

    [[nodiscard]] cairo_font_face_t* normal() const noexcept {
        return font_normal_.get();
    }
    [[nodiscard]] cairo_font_face_t* bold() const noexcept {
        return font_bold_.get();
    }

    void reload() {
        font_normal_ = create_font_face_with_fallback(CAIRO_FONT_WEIGHT_NORMAL);
        font_bold_ = create_font_face_with_fallback(CAIRO_FONT_WEIGHT_BOLD);
    }

  private:
    SharedCairoFonts() {
        reload();
    }

    UniqueCairoFontFace font_normal_;
    UniqueCairoFontFace font_bold_;
};

class CairoEngineImpl final : public PdfEngine::Impl {
  public:
    CairoEngineImpl() {
        // Eagerly ensure shared fonts are front-loaded at startup
        (void)SharedCairoFonts::instance();
    }

    [[nodiscard]] std::expected<std::vector<uint8_t>, RenderError> render(const DocumentLayout& layout,
                                                                          size_t tail_capacity) const override {
        std::vector<uint8_t> buffer;

        struct Output {
            std::vector<uint8_t>& bytes;
            size_t tail_capacity;
        } output{buffer, tail_capacity};
        auto write_callback = [](void* closure, const unsigned char* data, unsigned int length) -> cairo_status_t {
            auto& out = *static_cast<Output*>(closure);
            try {
                const size_t required = out.bytes.size() + length + out.tail_capacity;
                if (required > out.bytes.capacity()) {
                    out.bytes.reserve(std::max(required, out.bytes.capacity() * 2));
                }
                out.bytes.insert(out.bytes.end(), data, data + length);
                return CAIRO_STATUS_SUCCESS;
            } catch (...) {
                // Never unwind a C++ exception through Cairo's C callback frames.
                return CAIRO_STATUS_WRITE_ERROR;
            }
        };

        const float default_w = layout.pages.empty() ? 612.0F : layout.pages.front().width;
        const float default_h = layout.pages.empty() ? 792.0F : layout.pages.front().height;

        UniqueCairoSurface surface(cairo_pdf_surface_create_for_stream(write_callback, &output, default_w, default_h));
        if (!surface || cairo_surface_status(surface.get()) != CAIRO_STATUS_SUCCESS) {
            return std::unexpected(RenderError{
                .kind = RenderErrorKind::EngineError,
                .message = "cairo: failed to create PDF surface",
            });
        }

        // Keep the fresh renderer output on classic xref tables, shared with Haru
        // and Native. The signing update advertises PDF 1.7 in the catalog.
        cairo_pdf_surface_restrict_to_version(surface.get(), CAIRO_PDF_VERSION_1_4);

        UniqueCairo cr(cairo_create(surface.get()));
        if (!cr || cairo_status(cr.get()) != CAIRO_STATUS_SUCCESS) {
            return std::unexpected(RenderError{
                .kind = RenderErrorKind::EngineError,
                .message = "cairo: failed to create drawing context",
            });
        }

        cairo_set_source_rgb(cr.get(), 0.0, 0.0, 0.0);

        const auto& fonts = SharedCairoFonts::instance();
        if (!fonts.is_valid()) {
            return std::unexpected(RenderError{
                .kind = RenderErrorKind::EngineError,
                .message = "cairo: failed to resolve a valid font face (tried 'Helvetica', 'Arial', 'sans-serif')",
            });
        }

        // Surfaces are local to a render; immutable source pixels are shared.
        std::unordered_map<const RasterImage*, UniqueCairoSurface> images;
        for (const PageLayout& page_layout : layout.pages) {
            cairo_pdf_surface_set_size(surface.get(), page_layout.width, page_layout.height);

            for (const auto& command : page_layout.commands) {
                std::visit(
                    [&](const auto& item) {
                        using T = std::decay_t<decltype(item)>;
                        cairo_save(cr.get());
                        if constexpr (std::is_same_v<T, PositionedText>) {
                            cairo_set_source_rgb(cr.get(), item.color.red, item.color.green, item.color.blue);
                            cairo_set_font_face(cr.get(),
                                                item.weight == FontWeight::Bold ? fonts.bold() : fonts.normal());
                            cairo_set_font_size(cr.get(), item.font_size);
                            cairo_move_to(cr.get(), item.x, item.y);
                            const auto value = display_text(item.text);
                            cairo_show_text(cr.get(), value.c_str());
                        } else if constexpr (std::is_same_v<T, PositionedLine>) {
                            cairo_set_source_rgb(cr.get(), item.color.red, item.color.green, item.color.blue);
                            cairo_set_line_width(cr.get(), item.line_width);
                            cairo_move_to(cr.get(), item.x1, item.y1);
                            cairo_line_to(cr.get(), item.x2, item.y2);
                            cairo_stroke(cr.get());
                        } else if constexpr (std::is_same_v<T, FilledRectangle>) {
                            cairo_set_source_rgb(cr.get(), item.color.red, item.color.green, item.color.blue);
                            cairo_rectangle(cr.get(), item.x, item.y, item.width, item.height);
                            cairo_fill(cr.get());
                        } else {
                            auto [entry, inserted] = images.try_emplace(item.image);
                            if (inserted) {
                                entry->second.reset(cairo_image_surface_create(CAIRO_FORMAT_RGB24,
                                                                               static_cast<int>(item.image->width),
                                                                               static_cast<int>(item.image->height)));
                                if (cairo_surface_status(entry->second.get()) == CAIRO_STATUS_SUCCESS) {
                                    auto* pixels = cairo_image_surface_get_data(entry->second.get());
                                    const int stride = cairo_image_surface_get_stride(entry->second.get());
                                    for (uint32_t y = 0; y < item.image->height; ++y) {
                                        auto* row = reinterpret_cast<uint32_t*>(pixels + y * stride);
                                        for (uint32_t x = 0; x < item.image->width; ++x) {
                                            const size_t offset = (static_cast<size_t>(y) * item.image->width + x) * 3;
                                            row[x] = (uint32_t(item.image->rgb[offset]) << 16) |
                                                     (uint32_t(item.image->rgb[offset + 1]) << 8) |
                                                     uint32_t(item.image->rgb[offset + 2]);
                                        }
                                    }
                                    cairo_surface_mark_dirty(entry->second.get());
                                }
                            }
                            cairo_translate(cr.get(), item.x, item.y);
                            cairo_scale(cr.get(), item.width / item.image->width, item.height / item.image->height);
                            cairo_set_source_surface(cr.get(), entry->second.get(), 0, 0);
                            cairo_pattern_set_filter(cairo_get_source(cr.get()), CAIRO_FILTER_BILINEAR);
                            cairo_rectangle(cr.get(), 0, 0, item.image->width, item.image->height);
                            cairo_fill(cr.get());
                        }
                        cairo_restore(cr.get());
                    },
                    command);
            }

            cairo_show_page(cr.get());
        }

        const cairo_status_t cr_status = cairo_status(cr.get());
        cr.reset();
        if (cr_status != CAIRO_STATUS_SUCCESS) {
            return std::unexpected(RenderError{
                .kind = RenderErrorKind::EngineError,
                .message = std::string("cairo: drawing context failed: ") + cairo_status_to_string(cr_status),
            });
        }

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

std::unique_ptr<PdfEngine::Impl> create_cairo_pdf_engine() {
    return std::make_unique<CairoEngineImpl>();
}

namespace test {
void reload_cairo_fonts() {
    (void)SharedCairoFonts::instance(true);
}
} // namespace test

} // namespace nordiska
