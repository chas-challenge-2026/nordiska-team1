#include "nordiska/rendering/pdf_engine.hpp"

#include <cairo-pdf.h>
#include <cairo.h>
#include <cstdint>
#include <hpdf.h>
#include <memory>
#include <sstream>
#include <stdexcept>
#include <string>
#include <vector>

namespace nordiska {

struct PdfEngine::Impl {
    virtual ~Impl() = default;
    [[nodiscard]] virtual std::expected<std::vector<uint8_t>, RenderError>
    render(const DocumentLayout& layout) const = 0;
};

namespace {

// UTF-8 to CP1252 conversion for Swedish banking document characters in libharu
std::string utf8_to_cp1252(std::string_view utf8) {
    std::string result;
    result.reserve(utf8.size());
    for (std::size_t index = 0; index < utf8.size(); ++index) {
        const auto character = static_cast<unsigned char>(utf8[index]);
        if (character < 0x80) {
            result.push_back(static_cast<char>(character));
        } else if (character == 0xC2 && index + 1 < utf8.size()) {
            result.push_back(static_cast<char>(static_cast<unsigned char>(utf8[++index])));
        } else if (character == 0xC3 && index + 1 < utf8.size()) {
            result.push_back(static_cast<char>(static_cast<unsigned char>(utf8[++index]) + 0x40));
        } else if (character == 0xE2 && index + 2 < utf8.size() &&
                   static_cast<unsigned char>(utf8[index + 1]) == 0x82 &&
                   static_cast<unsigned char>(utf8[index + 2]) == 0xAC) {
            result.push_back(static_cast<char>(0x80)); // Euro symbol
            index += 2;
        } else {
            result.push_back('?');
        }
    }
    return result;
}

// --- Libharu Engine Implementation ---

struct HaruDocDeleter {
    void operator()(HPDF_Doc doc) const noexcept {
        if (doc != nullptr) {
            HPDF_Free(doc);
        }
    }
};
using UniqueHaruDoc = std::unique_ptr<std::remove_pointer_t<HPDF_Doc>, HaruDocDeleter>;

struct HaruErrorData {
    HPDF_STATUS code{HPDF_OK};
    HPDF_STATUS detail{0};
};

void haru_error_handler(HPDF_STATUS error_no, HPDF_STATUS detail_no, void* user_data) {
    auto* error = static_cast<HaruErrorData*>(user_data);
    error->code = error_no;
    error->detail = detail_no;
}

class HaruEngineImpl final : public PdfEngine::Impl {
  public:
    [[nodiscard]] std::expected<std::vector<uint8_t>, RenderError> render(const DocumentLayout& layout) const override {
        HaruErrorData error;
        UniqueHaruDoc pdf(HPDF_New(haru_error_handler, &error));
        if (!pdf) {
            return std::unexpected(RenderError{
                .kind = RenderErrorKind::EngineError,
                .message = "libharu: failed to create document handle",
            });
        }

        HPDF_SetCompressionMode(pdf.get(), HPDF_COMP_ALL);

        HPDF_Font font_normal = HPDF_GetFont(pdf.get(), "Helvetica", "CP1252");
        if (font_normal == nullptr) {
            HPDF_ResetError(pdf.get());
            font_normal = HPDF_GetFont(pdf.get(), "Helvetica", nullptr);
        }

        HPDF_Font font_bold = HPDF_GetFont(pdf.get(), "Helvetica-Bold", "CP1252");
        if (font_bold == nullptr) {
            HPDF_ResetError(pdf.get());
            font_bold = HPDF_GetFont(pdf.get(), "Helvetica-Bold", nullptr);
        }

        if (font_normal == nullptr || font_bold == nullptr) {
            return std::unexpected(RenderError{
                .kind = RenderErrorKind::EngineError,
                .message = "libharu: failed to load required Helvetica fonts",
            });
        }

        for (const PageLayout& page_layout : layout.pages) {
            HPDF_Page page = HPDF_AddPage(pdf.get());
            if (page == nullptr) {
                return std::unexpected(RenderError{
                    .kind = RenderErrorKind::EngineError,
                    .message = "libharu: failed to add page",
                });
            }

            HPDF_Page_SetWidth(page, page_layout.width);
            HPDF_Page_SetHeight(page, page_layout.height);

            // Draw Lines
            for (const PositionedLine& line : page_layout.lines) {
                HPDF_Page_SetLineWidth(page, line.line_width);
                HPDF_Page_MoveTo(page, line.x1, page_layout.height - line.y1);
                HPDF_Page_LineTo(page, line.x2, page_layout.height - line.y2);
                HPDF_Page_Stroke(page);
            }

            // Draw Texts
            HPDF_Page_BeginText(page);
            for (const PositionedText& text : page_layout.texts) {
                HPDF_Font active_font = text.weight == FontWeight::Bold ? font_bold : font_normal;
                HPDF_Page_SetFontAndSize(page, active_font, text.font_size);
                const float haru_y = page_layout.height - text.y;
                const std::string cp1252_str = utf8_to_cp1252(text.text);
                HPDF_Page_TextOut(page, text.x, haru_y, cp1252_str.c_str());
            }
            HPDF_Page_EndText(page);
        }

        if (HPDF_SaveToStream(pdf.get()) != HPDF_OK) {
            return std::unexpected(RenderError{
                .kind = RenderErrorKind::EngineError,
                .message = "libharu: failed to serialize PDF to stream",
            });
        }

        const HPDF_UINT32 stream_size = HPDF_GetStreamSize(pdf.get());
        std::vector<uint8_t> buffer(stream_size);
        HPDF_UINT32 read_bytes = stream_size;
        HPDF_ReadFromStream(pdf.get(), reinterpret_cast<HPDF_BYTE*>(buffer.data()), &read_bytes);

        return buffer;
    }
};

// --- Cairo Engine Implementation ---

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

PdfEngine::PdfEngine(PdfEngineKind kind) {
    switch (kind) {
    case PdfEngineKind::Libharu:
        impl_ = std::make_unique<HaruEngineImpl>();
        break;
    case PdfEngineKind::Cairo:
        impl_ = std::make_unique<CairoEngineImpl>();
        break;
    }
}

PdfEngine::~PdfEngine() = default;

PdfEngine::PdfEngine(PdfEngine&&) noexcept = default;
PdfEngine& PdfEngine::operator=(PdfEngine&&) noexcept = default;

std::expected<std::vector<uint8_t>, RenderError> PdfEngine::render(const DocumentLayout& layout) const {
    return impl_->render(layout);
}

} // namespace nordiska
