#include <filesystem>
#include <iostream>
#include <stdexcept>

#include "nordiska/create_pdf.hpp"
#include "nordiska/json_report_reader.hpp"
#include "nordiska/minimal_pdf_renderer.hpp"

int main(int argc, char* argv[]) {
    if (argc != 3) {
        std::cerr << "Usage: pdf_generator <input.json> <output.pdf>\n";
        return 2;
    }

    try {
        const nordiska::Report report =
            nordiska::read_report_json(std::filesystem::path(argv[1]));
        nordiska::MinimalPdfRenderer renderer;
        nordiska::CreatePdf create_pdf(renderer);

        create_pdf.execute(report, std::filesystem::path(argv[2]));
        std::cout << "Created " << argv[2] << "\n";
        return 0;
    } catch (const std::exception& error) {
        std::cerr << "PDF generation failed: " << error.what() << "\n";
        return 1;
    }
}

