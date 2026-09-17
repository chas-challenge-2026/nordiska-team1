#include "nordiska/rendering/pdf_engine.hpp"

#include <cairo-pdf.h>
#include <cairo.h>
#include <charconv>
#include <cstdint>
#include <format>
#include <hpdf.h>
#include <memory>
#include <sstream>
#include <stdexcept>
#include <string>
#include <vector>
#include <zlib.h>

namespace nordiska {

struct PdfEngine::Impl {
    virtual ~Impl() = default;
    [[nodiscard]] virtual std::expected<std::vector<uint8_t>, RenderError>
    render(const DocumentLayout& layout) const = 0;
};

namespace {

thread_local std::string t_cp1252_scratch;

bool is_pure_ascii(std::string_view sv) noexcept {
    for (unsigned char c : sv) {
        if (c >= 0x80) {
            return false;
        }
    }
    return true;
}

// UTF-8 to CP1252 conversion for Swedish banking document characters in libharu
void utf8_to_cp1252_append(std::string_view utf8, std::string& out) {
    out.reserve(out.size() + utf8.size());
    for (std::size_t index = 0; index < utf8.size(); ++index) {
        const auto character = static_cast<unsigned char>(utf8[index]);
        if (character < 0x80) {
            out.push_back(static_cast<char>(character));
        } else if (character == 0xC3 && index + 1 < utf8.size()) {
            out.push_back(static_cast<char>(static_cast<unsigned char>(utf8[++index]) + 0x40));
        } else if (character == 0xC2 && index + 1 < utf8.size()) {
            out.push_back(static_cast<char>(static_cast<unsigned char>(utf8[++index])));
        } else if (character == 0xE2 && index + 2 < utf8.size() &&
                   static_cast<unsigned char>(utf8[index + 1]) == 0x82 &&
                   static_cast<unsigned char>(utf8[index + 2]) == 0xAC) {
            out.push_back(static_cast<char>(0x80)); // Euro symbol
            index += 2;
        } else {
            out.push_back('?');
        }
    }
}

std::string utf8_to_cp1252(std::string_view utf8) {
    if (is_pure_ascii(utf8)) {
        return std::string(utf8);
    }
    std::string result;
    utf8_to_cp1252_append(utf8, result);
    return result;
}

inline void append_pdf_char(std::string& dest, char c) {
    if (c == '(' || c == ')' || c == '\\') {
        dest.push_back('\\');
    }
    dest.push_back(c);
}

// Single-pass UTF-8 -> CP1252 conversion with PDF string escaping directly into target stream
void append_pdf_escaped_text(std::string& dest, std::string_view utf8) {
    if (is_pure_ascii(utf8)) {
        for (char c : utf8) {
            append_pdf_char(dest, c);
        }
        return;
    }

    for (std::size_t index = 0; index < utf8.size(); ++index) {
        const auto character = static_cast<unsigned char>(utf8[index]);
        if (character < 0x80) {
            append_pdf_char(dest, static_cast<char>(character));
        } else if (character == 0xC3 && index + 1 < utf8.size()) {
            dest.push_back(static_cast<char>(static_cast<unsigned char>(utf8[++index]) + 0x40));
        } else if (character == 0xC2 && index + 1 < utf8.size()) {
            dest.push_back(static_cast<char>(static_cast<unsigned char>(utf8[++index])));
        } else if (character == 0xE2 && index + 2 < utf8.size() &&
                   static_cast<unsigned char>(utf8[index + 1]) == 0x82 &&
                   static_cast<unsigned char>(utf8[index + 2]) == 0xAC) {
            dest.push_back(static_cast<char>(0x80)); // Euro symbol in CP1252
            index += 2;
        } else {
            dest.push_back('?');
        }
    }
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

struct HaruBumpArena {
    static constexpr size_t kInitialCapacity = 512 * 1024; // 512 KB
    std::vector<uint8_t> buffer;
    size_t offset{0};

    HaruBumpArena() : buffer(kInitialCapacity) {}

    void reset() noexcept {
        offset = 0;
    }

    void* allocate(size_t bytes) {
        const size_t aligned_offset = (offset + 7) & ~static_cast<size_t>(7);
        if (aligned_offset + bytes > buffer.size()) {
            buffer.resize(std::max(buffer.size() * 2, aligned_offset + bytes + 65536));
        }
        offset = aligned_offset + bytes;
        return buffer.data() + aligned_offset;
    }
};

thread_local HaruBumpArena t_haru_arena;

void* HPDF_STDCALL haru_arena_alloc(HPDF_UINT size) {
    return t_haru_arena.allocate(size);
}

void HPDF_STDCALL haru_arena_free(void* /*aptr*/) {
    // No-op: bump arena memory reclaimed in O(1) via t_haru_arena.reset()
}

class HaruEngineImpl final : public PdfEngine::Impl {
  public:
    explicit HaruEngineImpl(bool compression = true) : compression_(compression) {}

    [[nodiscard]] std::expected<std::vector<uint8_t>, RenderError> render(const DocumentLayout& layout) const override {
        HaruErrorData error;
        t_haru_arena.reset();
        UniqueHaruDoc pdf(HPDF_NewEx(haru_error_handler, haru_arena_alloc, haru_arena_free, 0, &error));
        if (!pdf) {
            return std::unexpected(RenderError{
                .kind = RenderErrorKind::EngineError,
                .message = "libharu: failed to create document handle",
            });
        }

        HPDF_SetCompressionMode(pdf.get(), compression_ ? HPDF_COMP_ALL : HPDF_COMP_NONE);

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
                if (is_pure_ascii(text.text)) {
                    HPDF_Page_TextOut(page, text.x, haru_y, text.text.c_str());
                } else {
                    t_cp1252_scratch.clear();
                    utf8_to_cp1252_append(text.text, t_cp1252_scratch);
                    HPDF_Page_TextOut(page, text.x, haru_y, t_cp1252_scratch.c_str());
                }
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

  private:
    bool compression_{true};
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

inline void append_float_2(std::string& out, float val) {
    char buf[32];
    auto [ptr, ec] = std::to_chars(buf, buf + sizeof(buf), val, std::chars_format::fixed, 2);
    out.append(buf, ptr - buf);
}

// --- Dedicated Minimal Native PDF Engine Implementation (Solution B) ---

class NativeEngineImpl final : public PdfEngine::Impl {
  public:
    explicit NativeEngineImpl(bool compression = true) : compression_(compression) {}

    [[nodiscard]] std::expected<std::vector<uint8_t>, RenderError> render(const DocumentLayout& layout) const override {
        const size_t num_pages = layout.pages.size();
        if (num_pages == 0) {
            return std::unexpected(RenderError{
                .kind = RenderErrorKind::EngineError,
                .message = "native_engine: document has no pages",
            });
        }

        // Object ID layout:
        // Object 1: Catalog
        // Object 2: Pages root
        // For page i (0 <= i < num_pages):
        //   Page obj:               3 + 2 * i
        //   Content stream obj:     4 + 2 * i
        // Font F1 (Helvetica):      3 + 2 * num_pages
        // Font F2 (Helvetica-Bold): 4 + 2 * num_pages
        const size_t total_objects = 4 + 2 * num_pages;
        const size_t font_f1_id = 3 + 2 * num_pages;
        const size_t font_f2_id = 4 + 2 * num_pages;

        std::vector<size_t> object_offsets(total_objects + 1, 0);
        std::vector<uint8_t> pdf;
        pdf.reserve(8192 * num_pages);

        auto append_string = [&](std::string_view sv) { pdf.insert(pdf.end(), sv.begin(), sv.end()); };

        // 1. Header with binary marker comment
        pdf.insert(pdf.end(),
                   {'%', 'P', 'D', 'F', '-', '1', '.', '4', '\n', '%', static_cast<uint8_t>(0xE2),
                    static_cast<uint8_t>(0xE3), static_cast<uint8_t>(0xCF), static_cast<uint8_t>(0xD3), '\n'});

        // 2. Object 1: Catalog
        object_offsets[1] = pdf.size();
        append_string("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        // 3. Object 2: Pages root
        object_offsets[2] = pdf.size();
        std::string kids_str;
        for (size_t i = 0; i < num_pages; ++i) {
            kids_str += std::format("{} 0 R ", 3 + 2 * i);
        }
        append_string(std::format("2 0 obj\n<< /Type /Pages /Kids [ {}] /Count {} >>\nendobj\n", kids_str, num_pages));

        // Thread-local scratch buffers for content streams and compression
        thread_local std::string t_stream_buf;
        thread_local std::vector<uint8_t> t_compress_buf;

        // 4. Per-page objects
        for (size_t i = 0; i < num_pages; ++i) {
            const auto& page = layout.pages[i];
            const size_t page_obj_id = 3 + 2 * i;
            const size_t content_obj_id = 4 + 2 * i;

            // Page Object
            object_offsets[page_obj_id] = pdf.size();
            append_string(std::format("{} 0 obj\n"
                                      "<< /Type /Page\n"
                                      "   /Parent 2 0 R\n"
                                      "   /MediaBox [ 0 0 {:.2f} {:.2f} ]\n"
                                      "   /Contents {} 0 R\n"
                                      "   /Resources <<\n"
                                      "     /Font << /F1 {} 0 R /F2 {} 0 R >>\n"
                                      "     /ProcSet [ /PDF /Text ]\n"
                                      "   >>\n"
                                      ">>\nendobj\n",
                                      page_obj_id, page.width, page.height, content_obj_id, font_f1_id, font_f2_id));

            // Generate content stream
            t_stream_buf.clear();

            // Draw Lines
            if (!page.lines.empty()) {
                float current_width = -1.0F;
                for (const PositionedLine& line : page.lines) {
                    if (line.line_width != current_width) {
                        current_width = line.line_width;
                        append_float_2(t_stream_buf, current_width);
                        t_stream_buf.append(" w\n");
                    }
                    const float y1 = page.height - line.y1;
                    const float y2 = page.height - line.y2;
                    append_float_2(t_stream_buf, line.x1);
                    t_stream_buf.push_back(' ');
                    append_float_2(t_stream_buf, y1);
                    t_stream_buf.append(" m ");
                    append_float_2(t_stream_buf, line.x2);
                    t_stream_buf.push_back(' ');
                    append_float_2(t_stream_buf, y2);
                    t_stream_buf.append(" l S\n");
                }
            }

            // Draw Texts
            if (!page.texts.empty()) {
                t_stream_buf.append("BT\n");
                std::string_view current_font = "";
                float current_font_size = -1.0F;

                for (const PositionedText& text : page.texts) {
                    std::string_view font_tag = (text.weight == FontWeight::Bold) ? "/F2" : "/F1";
                    if (font_tag != current_font || text.font_size != current_font_size) {
                        current_font = font_tag;
                        current_font_size = text.font_size;
                        t_stream_buf.append(current_font);
                        t_stream_buf.push_back(' ');
                        append_float_2(t_stream_buf, current_font_size);
                        t_stream_buf.append(" Tf\n");
                    }
                    const float haru_y = page.height - text.y;
                    t_stream_buf.append("1 0 0 1 ");
                    append_float_2(t_stream_buf, text.x);
                    t_stream_buf.push_back(' ');
                    append_float_2(t_stream_buf, haru_y);
                    t_stream_buf.append(" Tm (");

                    append_pdf_escaped_text(t_stream_buf, text.text);
                    t_stream_buf.append(") Tj\n");
                }
                t_stream_buf.append("ET\n");
            }

            // Write Content Stream Object
            object_offsets[content_obj_id] = pdf.size();
            if (compression_) {
                uLongf dest_len = compressBound(t_stream_buf.size());
                if (t_compress_buf.size() < dest_len) {
                    t_compress_buf.resize(dest_len);
                }
                if (compress(t_compress_buf.data(), &dest_len, reinterpret_cast<const Bytef*>(t_stream_buf.data()),
                             t_stream_buf.size()) == Z_OK) {
                    append_string(std::format("{} 0 obj\n<< /Length {} /Filter /FlateDecode >>\nstream\n",
                                              content_obj_id, dest_len));
                    pdf.insert(pdf.end(), t_compress_buf.data(), t_compress_buf.data() + dest_len);
                    append_string("\nendstream\nendobj\n");
                } else {
                    append_string(
                        std::format("{} 0 obj\n<< /Length {} >>\nstream\n", content_obj_id, t_stream_buf.size()));
                    append_string(t_stream_buf);
                    append_string("\nendstream\nendobj\n");
                }
            } else {
                append_string(std::format("{} 0 obj\n<< /Length {} >>\nstream\n", content_obj_id, t_stream_buf.size()));
                append_string(t_stream_buf);
                append_string("\nendstream\nendobj\n");
            }
        }

        // 5. Font F1 (Helvetica)
        object_offsets[font_f1_id] = pdf.size();
        append_string(std::format("{} 0 obj\n"
                                  "<< /Type /Font\n"
                                  "   /Subtype /Type1\n"
                                  "   /BaseFont /Helvetica\n"
                                  "   /Encoding /WinAnsiEncoding\n"
                                  ">>\nendobj\n",
                                  font_f1_id));

        // 6. Font F2 (Helvetica-Bold)
        object_offsets[font_f2_id] = pdf.size();
        append_string(std::format("{} 0 obj\n"
                                  "<< /Type /Font\n"
                                  "   /Subtype /Type1\n"
                                  "   /BaseFont /Helvetica-Bold\n"
                                  "   /Encoding /WinAnsiEncoding\n"
                                  ">>\nendobj\n",
                                  font_f2_id));

        // 7. Cross-reference Table (xref)
        const size_t xref_offset = pdf.size();
        append_string(std::format("xref\n0 {}\n", total_objects + 1));
        append_string("0000000000 65535 f \n");
        for (size_t id = 1; id <= total_objects; ++id) {
            append_string(std::format("{:010d} 00000 n \n", object_offsets[id]));
        }

        // 8. Trailer
        append_string(std::format("trailer\n"
                                  "<< /Size {}\n"
                                  "   /Root 1 0 R\n"
                                  ">>\n"
                                  "startxref\n"
                                  "{}\n"
                                  "%%EOF\n",
                                  total_objects + 1, xref_offset));

        return pdf;
    }

  private:
    bool compression_{true};
};

} // namespace

PdfEngine::PdfEngine(PdfEngineConfig config) {
    switch (config.kind) {
    case PdfEngineKind::Libharu:
        impl_ = std::make_unique<HaruEngineImpl>(config.compression);
        break;
    case PdfEngineKind::Cairo:
        impl_ = std::make_unique<CairoEngineImpl>();
        break;
    case PdfEngineKind::Native:
        impl_ = std::make_unique<NativeEngineImpl>(config.compression);
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
