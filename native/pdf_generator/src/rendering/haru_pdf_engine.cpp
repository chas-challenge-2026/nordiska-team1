#include "nordiska/rendering/haru_pdf_engine.hpp"

#include <algorithm>
#include <cstdint>
#include <hpdf.h>
#include <memory>
#include <string>
#include <string_view>
#include <vector>

namespace nordiska {

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

    [[nodiscard]] std::expected<std::vector<uint8_t>, RenderError> render(const DocumentLayout& layout,
                                                                          size_t tail_capacity) const override {
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
        // Include the caller's tail reservation in the initial output allocation.
        std::vector<uint8_t> buffer;
        buffer.reserve(static_cast<size_t>(stream_size) + tail_capacity);
        buffer.resize(stream_size);
        HPDF_UINT32 read_bytes = stream_size;
        HPDF_ReadFromStream(pdf.get(), reinterpret_cast<HPDF_BYTE*>(buffer.data()), &read_bytes);

        return buffer;
    }

  private:
    bool compression_{true};
};

} // namespace

std::unique_ptr<PdfEngine::Impl> create_haru_pdf_engine(bool compression) {
    return std::make_unique<HaruEngineImpl>(compression);
}

} // namespace nordiska
