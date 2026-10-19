#include "nordiska/rendering/haru_pdf_engine.hpp"

#include "nordiska/rendering/utf8_to_cp1252.hpp"

#include <algorithm>
#include <cctype>
#include <cerrno>
#include <cstdint>
#include <cstdlib>
#include <hpdf.h>
#include <memory>
#include <string>
#include <string_view>
#include <vector>

namespace nordiska {

namespace {

thread_local std::string t_cp1252_scratch;

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

inline size_t parse_env_size(const char* env_var_name, size_t default_value) noexcept {
    if (const char* val = std::getenv(env_var_name); val != nullptr && *val != '\0') {
        const char* p = val;
        while (*p != '\0' && std::isspace(static_cast<unsigned char>(*p))) {
            ++p;
        }
        if (*p == '-' || *p == '\0') {
            return default_value;
        }
        char* end = nullptr;
        errno = 0;
        const unsigned long long parsed = std::strtoull(p, &end, 10);
        if (errno == 0 && end != p && *end == '\0' && parsed > 0) {
            return static_cast<size_t>(parsed);
        }
    }
    return default_value;
}

constexpr size_t kDefaultHaruBaseChunkBytes = 2 * 1024 * 1024; // 2 MB baseline
constexpr size_t kDefaultHaruMaxArenaBytes = 32 * 1024 * 1024; // 32 MB hard ceiling

struct Chunk {
    std::unique_ptr<uint8_t[]> data;
    size_t capacity{0};
    size_t offset{0};

    Chunk() = default;
    explicit Chunk(size_t cap) : data(new uint8_t[cap]), capacity(cap), offset(0) {}
};

struct HaruBumpArena {
    Chunk base_chunk;
    std::vector<Chunk> overflow_chunks;
    Chunk* active_chunk{nullptr};
    size_t total_capacity{0};
    size_t max_arena_bytes{kDefaultHaruMaxArenaBytes};
    bool budget_exceeded{false};

    HaruBumpArena() {
        init_base();
    }

    void init_base() {
        const size_t configured_base = parse_env_size("NORDISKA_PDF_HARU_BASE_CHUNK_BYTES", kDefaultHaruBaseChunkBytes);
        max_arena_bytes = parse_env_size("NORDISKA_PDF_HARU_MAX_ARENA_BYTES", kDefaultHaruMaxArenaBytes);
        const size_t effective_base = std::min(configured_base, max_arena_bytes);

        base_chunk = Chunk(effective_base);
        base_chunk.offset = 0;
        active_chunk = &base_chunk;
        total_capacity = base_chunk.capacity;
        overflow_chunks.clear();
        overflow_chunks.reserve(8);
        budget_exceeded = false;
    }

    void reset() noexcept {
        const size_t configured_base = parse_env_size("NORDISKA_PDF_HARU_BASE_CHUNK_BYTES", kDefaultHaruBaseChunkBytes);
        max_arena_bytes = parse_env_size("NORDISKA_PDF_HARU_MAX_ARENA_BYTES", kDefaultHaruMaxArenaBytes);
        const size_t effective_base = std::min(configured_base, max_arena_bytes);

        // Deallocate all overflow chunks immediately back to the OS
        overflow_chunks.clear();
        overflow_chunks.shrink_to_fit();
        overflow_chunks.reserve(8);

        if (base_chunk.capacity != effective_base) {
            try {
                base_chunk = Chunk(effective_base);
            } catch (...) {
                // If reallocation fails under extreme pressure, keep existing base chunk
            }
        }

        base_chunk.offset = 0;
        active_chunk = &base_chunk;
        total_capacity = base_chunk.capacity;
        budget_exceeded = false;
    }

    [[nodiscard]] size_t get_planned_overflow_size(size_t overflow_idx) const noexcept {
        switch (overflow_idx) {
        case 0:
            return 4 * 1024 * 1024; // Overflow 1: 4 MB (Total = 6 MB)
        case 1:
            return 6 * 1024 * 1024; // Overflow 2: 6 MB (Total = 12 MB)
        case 2:
            return 20 * 1024 * 1024; // Overflow 3: 20 MB (Total = 32 MB)
        default:
            // For ceilings configured beyond 32 MB via env var, continue geometric doubling
            if (!overflow_chunks.empty()) {
                return overflow_chunks.back().capacity * 2;
            }
            return 20 * 1024 * 1024;
        }
    }

    void* allocate(size_t bytes) noexcept {
        if (bytes == 0) {
            bytes = 1;
        }

        if (active_chunk == nullptr) {
            return nullptr;
        }

        // Align current offset to 8 bytes (matching Libharu struct alignment)
        const size_t aligned_offset = (active_chunk->offset + 7) & ~static_cast<size_t>(7);

        // Check if allocation fits in current active chunk
        if (aligned_offset <= active_chunk->capacity && bytes <= active_chunk->capacity - aligned_offset) {
            active_chunk->offset = aligned_offset + bytes;
            return active_chunk->data.get() + aligned_offset;
        }

        // Check if arena budget is already exhausted or insufficient for this allocation
        if (total_capacity >= max_arena_bytes) {
            budget_exceeded = true;
            return nullptr;
        }

        const size_t remaining_budget = max_arena_bytes - total_capacity;
        if (bytes > remaining_budget) {
            budget_exceeded = true;
            return nullptr;
        }

        // Geometric growth progression:
        // Overflow Chunk 1: 4 MB (Total = 6 MB)
        // Overflow Chunk 2: 6 MB (Total = 12 MB)
        // Overflow Chunk 3: 20 MB (Total = 32 MB)
        const size_t planned = get_planned_overflow_size(overflow_chunks.size());
        size_t new_chunk_capacity = std::max(planned, bytes);
        new_chunk_capacity = std::min(new_chunk_capacity, remaining_budget);

        try {
            overflow_chunks.emplace_back(new_chunk_capacity);
        } catch (...) {
            budget_exceeded = true;
            return nullptr;
        }

        total_capacity += new_chunk_capacity;
        active_chunk = &overflow_chunks.back();
        active_chunk->offset = bytes;
        return active_chunk->data.get();
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

        const auto make_render_error = [&](std::string default_msg) -> RenderError {
            if (error.code == HPDF_FAILED_TO_ALLOC_MEM || error.code == HPDF_FAILD_TO_ALLOC_MEM ||
                t_haru_arena.budget_exceeded) {
                return RenderError{
                    .kind = RenderErrorKind::OutOfMemory,
                    .message = "libharu: memory arena budget exceeded",
                };
            }
            return RenderError{
                .kind = RenderErrorKind::EngineError,
                .message = std::move(default_msg),
            };
        };

        if (!pdf) {
            return std::unexpected(make_render_error("libharu: failed to create document handle"));
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
            return std::unexpected(make_render_error("libharu: failed to load required Helvetica fonts"));
        }

        for (const PageLayout& page_layout : layout.pages) {
            HPDF_Page page = HPDF_AddPage(pdf.get());
            if (page == nullptr) {
                return std::unexpected(make_render_error("libharu: failed to add page"));
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
                if (error.code == HPDF_FAILED_TO_ALLOC_MEM || error.code == HPDF_FAILD_TO_ALLOC_MEM ||
                    t_haru_arena.budget_exceeded) {
                    return std::unexpected(make_render_error("libharu: memory arena budget exceeded"));
                }
            }
            HPDF_Page_EndText(page);
        }

        if (HPDF_SaveToStream(pdf.get()) != HPDF_OK) {
            return std::unexpected(make_render_error("libharu: failed to serialize PDF to stream"));
        }

        if (error.code == HPDF_FAILED_TO_ALLOC_MEM || error.code == HPDF_FAILD_TO_ALLOC_MEM ||
            t_haru_arena.budget_exceeded) {
            return std::unexpected(make_render_error("libharu: memory arena budget exceeded"));
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

namespace test {

HaruArenaStats get_haru_arena_stats() {
    return HaruArenaStats{
        .total_capacity = t_haru_arena.total_capacity,
        .overflow_chunk_count = t_haru_arena.overflow_chunks.size(),
        .base_chunk_capacity = t_haru_arena.base_chunk.capacity,
        .budget_exceeded = t_haru_arena.budget_exceeded,
    };
}

void reset_haru_arena() {
    t_haru_arena.reset();
}

void* haru_arena_allocate_for_test(size_t bytes) {
    return t_haru_arena.allocate(bytes);
}

} // namespace test

} // namespace nordiska
