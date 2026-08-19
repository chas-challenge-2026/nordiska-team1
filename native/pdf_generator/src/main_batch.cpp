#include <algorithm>
#include <exception>
#include <filesystem>
#include <iostream>
#include <memory>
#include <stdexcept>
#include <string_view>
#include <vector>

#include "nordiska/batch_create_pdf.hpp"
#include "nordiska/cairo_pdf_renderer.hpp"
#include "nordiska/json_input_adapter.hpp"
#include "nordiska/libharu_pdf_renderer.hpp"
#include "renderer_output_path.hpp"

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
    if (argc < 2 || argc > 6) {
        std::cerr << "Usage: pdf_generator_batch <input-dir> "
                     "[workers] [--renderer haru|cairo]\n";
        return 2;
    }

    try {
        const std::filesystem::path input_directory(argv[1]);
        const std::filesystem::path output_directory = nordiska::cli::reports_directory();
        std::size_t worker_count = 0;
        std::string_view renderer_name = "haru";

        int argument = 2;
        if (argument < argc && std::string_view(argv[argument]) != "--renderer") {
            worker_count = std::stoull(argv[argument++]);
        }
        if (argument < argc) {
            if (std::string_view(argv[argument]) != "--renderer" || argument + 1 >= argc) {
                throw std::invalid_argument("renderer option must be --renderer haru|cairo");
            }
            renderer_name = argv[argument + 1];
            argument += 2;
        }
        if (argument != argc) {
            throw std::invalid_argument("unexpected batch argument");
        }

        if (!std::filesystem::is_directory(input_directory)) {
            throw std::runtime_error("input directory does not exist: " + input_directory.string());
        }
        std::filesystem::create_directories(output_directory);

        std::vector<std::filesystem::path> inputs;
        for (const auto& entry : std::filesystem::directory_iterator(input_directory)) {
            if (entry.is_regular_file() && entry.path().extension() == ".json") {
                inputs.push_back(entry.path());
            }
        }
        std::sort(inputs.begin(), inputs.end());

        const nordiska::JsonInputAdapter json_input_adapter;
        const nordiska::IInputAdapter& input_adapter = json_input_adapter;
        std::vector<nordiska::BatchPdfRequest> requests;
        requests.reserve(inputs.size());
        std::int64_t report_number =
            nordiska::cli::next_report_number(output_directory, renderer_name);
        for (const auto& input : inputs) {
            requests.push_back(
                {input_adapter.import(input),
                 nordiska::cli::report_path(output_directory, renderer_name, report_number++)});
        }

        nordiska::BatchCreatePdf batch([renderer_name] { return make_renderer(renderer_name); },
                                       worker_count);
        batch.execute(requests);
        std::cout << "Created " << requests.size() << " PDF(s)\n";
        return 0;
    } catch (const std::exception& error) {
        std::cerr << "Batch PDF generation failed: " << error.what() << "\n";
        return 1;
    }
}
