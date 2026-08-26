#include "nordiska/cairo_pdf_renderer.hpp"
#include "nordiska/create_pdf.hpp"
#include "nordiska/json_input_adapter.hpp"
#include "nordiska/libharu_pdf_renderer.hpp"
#include "renderer_output_path.hpp"

#include <exception>
#include <filesystem>
#include <iostream>
#include <memory>
#include <stdexcept>
#include <string_view>

namespace {

std::unique_ptr<nordiska::IPdfRenderer> make_renderer(std::string_view name) {
    if (name == "haru") {
        return std::make_unique<nordiska::LibHaruPdfRenderer>();
    }
    if (name == "cairo") {
        return std::make_unique<nordiska::CairoPdfRenderer>();
    }
    throw std::invalid_argument("renderer must be 'haru' or 'cairo'");
}

} // namespace

int main(int argc, char* argv[]) {
    if (argc < 2 || argc > 4) {
        std::cerr << "Usage: pdf_generator_single <input.json> "
                     "[--renderer haru|cairo]\n";
        return 2;
    }

    try {
        std::string_view renderer_name = "haru";
        if (argc > 2) {
            if (std::string_view(argv[2]) != "--renderer" || argc != 4) {
                throw std::invalid_argument("renderer option must be --renderer haru|cairo");
            }
            renderer_name = argv[3];
        }

        const nordiska::JsonInputAdapter json_input_adapter;
        const nordiska::IInputAdapter& input_adapter = json_input_adapter;
        const nordiska::Report report = input_adapter.import(std::filesystem::path(argv[1]));
        const std::filesystem::path output_path =
            nordiska::cli::next_report_path(nordiska::cli::reports_directory(), renderer_name);
        const std::unique_ptr<nordiska::IPdfRenderer> renderer = make_renderer(renderer_name);
        nordiska::CreatePdf create_pdf(*renderer);
        create_pdf.execute(report, output_path);
        std::cout << "Created " << output_path << "\n";
        return 0;
    } catch (const std::exception& error) {
        std::cerr << "PDF generation failed: " << error.what() << "\n";
        return 1;
    }
}
