#include "../pdf_engine.hpp"

#include <cstdint>
#include <hpdf.h>
#include <iomanip>
#include <memory>
#include <span>
#include <sstream>
#include <stdexcept>
#include <type_traits>
#include <vector>

namespace nordiska::pdf::detail {
namespace {

struct HaruError {
    HPDF_STATUS code{};
    HPDF_STATUS detail{};
};

void error_handler(HPDF_STATUS error_no, HPDF_STATUS detail_no, void* user_data) {
    auto* error = static_cast<HaruError*>(user_data);
    error->code = error_no;
    error->detail = detail_no;
}

void check(HaruError& error, HPDF_STATUS status, const char* operation) {
    if (status != HPDF_OK) {
        std::ostringstream message;
        message << "libharu " << operation << " failed (" << status << ", " << error.detail << ')';
        throw std::runtime_error(message.str());
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
            result.push_back(static_cast<char>(0x80));
            index += 2;
        } else {
            result.push_back('?');
        }
    }
    return result;
}

class HaruEngine final : public IPdfEngine {
  public:
    void render(const Document& document, IByteSink& sink) override {
        HaruError error;
        UniqueHaruDoc pdf(HPDF_New(error_handler, &error));
        if (!pdf) {
            throw std::runtime_error("libharu could not create a document");
        }

        check(error, HPDF_SetCompressionMode(pdf.get(), HPDF_COMP_ALL), "set compression");
        HPDF_Font font = HPDF_GetFont(pdf.get(), "Helvetica", "CP1252");
        if (font == nullptr) {
            HPDF_ResetError(pdf.get());
            font = HPDF_GetFont(pdf.get(), "Helvetica", nullptr);
        }
        if (font == nullptr) {
            throw std::runtime_error("libharu get font failed");
        }

        for (const Page& model_page : document.pages) {
            HPDF_Page page = HPDF_AddPage(pdf.get());
            if (page == nullptr) {
                throw std::runtime_error("libharu add page failed");
            }
            check(error, HPDF_Page_SetWidth(page, 612), "set page width");
            check(error, HPDF_Page_SetHeight(page, 792), "set page height");
            check(error, HPDF_Page_BeginText(page), "begin text");

            float y = 740;
            for (const TextLine& line : model_page.lines) {
                const float font_size = line.style == TextStyle::title ? 18.0F : 12.0F;
                check(error, HPDF_Page_SetFontAndSize(page, font, font_size), "set font");
                const std::string text = utf8_to_cp1252(line.text);
                check(error, HPDF_Page_TextOut(page, 72, y, text.c_str()), "write text");
                y -= line.style == TextStyle::title ? 30.0F : 18.0F;
            }
            check(error, HPDF_Page_EndText(page), "end text");
        }

        check(error, HPDF_SaveToStream(pdf.get()), "save document to stream");
        std::vector<std::byte> buffer(64 * 1024);
        while (true) {
            HPDF_UINT32 size = static_cast<HPDF_UINT32>(buffer.size());
            const HPDF_STATUS status =
                HPDF_ReadFromStream(pdf.get(), reinterpret_cast<HPDF_BYTE*>(buffer.data()), &size);
            if (status != HPDF_OK && status != HPDF_STREAM_EOF) {
                check(error, status, "read document stream");
            }
            if (size != 0) {
                sink.write(std::span<const std::byte>(buffer.data(), size));
            }
            if (status == HPDF_STREAM_EOF || size == 0) {
                break;
            }
        }
    }
};

} // namespace

std::unique_ptr<IPdfEngine> make_haru_engine() {
    return std::make_unique<HaruEngine>();
}

} // namespace nordiska::pdf::detail
