#include "nordiska/rendering/native_pdf_engine.hpp"

#include "nordiska/domain/generated_pdfs.hpp"

#include <charconv>
#include <cstdint>
#include <format>
#include <memory>
#include <string>
#include <string_view>
#include <vector>
#include <zlib.h>

namespace nordiska {

namespace {

bool is_pure_ascii(std::string_view sv) noexcept {
    for (unsigned char c : sv) {
        if (c >= 0x80) {
            return false;
        }
    }
    return true;
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

inline void append_float_2(std::string& out, float val) {
    char buf[32];
    auto [ptr, ec] = std::to_chars(buf, buf + sizeof(buf), val, std::chars_format::fixed, 2);
    out.append(buf, ptr - buf);
}

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

        // Ensure spare capacity for downstream digital signature block append so that
        // adding the /Sig dictionary and 8 KB placeholder incurs zero buffer reallocations.
        pdf.reserve(pdf.size() + kSignatureBlockSize);

        return pdf;
    }

  private:
    bool compression_{true};
};

} // namespace

std::unique_ptr<PdfEngine::Impl> create_native_pdf_engine(bool compression) {
    return std::make_unique<NativeEngineImpl>(compression);
}

} // namespace nordiska
