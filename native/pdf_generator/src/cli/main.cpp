#include "nordiska/adapters/input/json_input_adapter.hpp"
#include "nordiska/adapters/output/byte_sinks.hpp"
#include "nordiska/adapters/renderers/pdf/pdf_renderer.hpp"
#include "nordiska/application/generate_documents.hpp"
#include "nordiska/composition/default_composition.hpp"

#include <cstdlib>
#include <filesystem>
#include <iostream>
#include <memory>
#include <string>
#include <string_view>
#include <vector>

namespace {

struct CliOptions {
    std::filesystem::path input_path;
    std::filesystem::path output_path;
    std::string renderer = "haru";
    std::size_t workers = 0;
};

void print_usage(std::string_view program_name) {
    std::cout << "Usage: " << program_name << " <input-json> [options]\n\n"
              << "Options:\n"
              << "  -o, --output <path>    Output file or directory (default: current directory)\n"
              << "  --renderer <name>      Renderer engine: haru (default) or cairo\n"
              << "  --workers <count>      Worker threads (default: hardware concurrency)\n"
              << "  -h, --help             Show this help message\n";
}

CliOptions parse_cli(int argc, char* argv[]) {
    if (argc < 2) {
        print_usage(argv[0]);
        std::exit(EXIT_FAILURE);
    }

    CliOptions options;
    for (int index = 1; index < argc; ++index) {
        const std::string_view argument = argv[index];
        if (argument == "-h" || argument == "--help") {
            print_usage(argv[0]);
            std::exit(EXIT_SUCCESS);
        }
        if (argument == "-o" || argument == "--output") {
            if (++index >= argc) {
                std::cerr << "Error: " << argument << " requires a path value\n";
                std::exit(EXIT_FAILURE);
            }
            options.output_path = argv[index];
        } else if (argument == "--renderer") {
            if (++index >= argc) {
                std::cerr << "Error: --renderer requires a value\n";
                std::exit(EXIT_FAILURE);
            }
            options.renderer = argv[index];
        } else if (argument == "--workers") {
            if (++index >= argc) {
                std::cerr << "Error: --workers requires an integer value\n";
                std::exit(EXIT_FAILURE);
            }
            options.workers = std::stoull(argv[index]);
        } else if (argument.starts_with("-")) {
            std::cerr << "Error: unknown option: " << argument << "\n";
            print_usage(argv[0]);
            std::exit(EXIT_FAILURE);
        } else {
            if (!options.input_path.empty()) {
                std::cerr << "Error: unexpected argument: " << argument << "\n";
                std::exit(EXIT_FAILURE);
            }
            options.input_path = argv[index];
        }
    }

    if (options.input_path.empty()) {
        std::cerr << "Error: input JSON file is required\n";
        print_usage(argv[0]);
        std::exit(EXIT_FAILURE);
    }
    return options;
}

std::unique_ptr<nordiska::IDocumentRenderer> make_renderer(std::string_view name) {
    if (name == "haru") {
        return nordiska::make_pdf_renderer(nordiska::PdfEngine::haru);
    }
    if (name == "cairo") {
        return nordiska::make_pdf_renderer(nordiska::PdfEngine::cairo);
    }
    throw std::invalid_argument("unsupported renderer: " + std::string(name));
}

} // namespace

int main(int argc, char* argv[]) {
    try {
        const CliOptions options = parse_cli(argc, argv);

        if (!std::filesystem::exists(options.input_path)) {
            std::cerr << "Error: input file does not exist: " << options.input_path << "\n";
            return EXIT_FAILURE;
        }

        const nordiska::JsonInputAdapter input_adapter;
        const auto reports = input_adapter.import_reports(options.input_path);
        if (reports.empty()) {
            std::cerr << "Error: no reports found in " << options.input_path << "\n";
            return EXIT_FAILURE;
        }

        std::vector<nordiska::DocumentRequest> requests;
        requests.reserve(reports.size());
        for (const auto& report : reports) {
            requests.push_back({report});
        }

        std::filesystem::path output_directory = ".";
        bool single_file_mode = false;
        std::filesystem::path single_output_file;

        if (!options.output_path.empty()) {
            if (reports.size() == 1 && options.output_path.extension() == ".pdf") {
                single_file_mode = true;
                single_output_file = options.output_path;
                if (single_output_file.has_parent_path()) {
                    std::filesystem::create_directories(single_output_file.parent_path());
                }
            } else {
                output_directory = options.output_path;
                std::filesystem::create_directories(output_directory);
            }
        }

        const auto path_factory = [&](std::size_t index) -> std::filesystem::path {
            if (single_file_mode) {
                return single_output_file;
            }
            const std::string& account = reports[index].account_number;
            const std::string filename = account.empty() ? ("report-" + std::to_string(index) + ".pdf")
                                                         : (account + "-" + std::to_string(index) + ".pdf");
            return output_directory / filename;
        };

        nordiska::FileOutputDestination destination(path_factory);
        nordiska::GenerateDocuments generator([renderer = options.renderer] { return make_renderer(renderer); },
                                              options.workers);

        const auto results = generator.execute(requests, destination);

        std::size_t failures = 0;
        for (const auto& result : results) {
            if (!result.succeeded) {
                ++failures;
                std::cerr << "Error: report " << result.index << " failed: " << result.error << "\n";
            }
        }

        if (failures > 0) {
            std::cerr << "Generated " << (results.size() - failures) << " of " << results.size() << " documents ("
                      << failures << " failed)\n";
            return EXIT_FAILURE;
        }

        std::cout << "Successfully generated " << results.size() << " document(s)\n";
        return EXIT_SUCCESS;
    } catch (const std::exception& error) {
        std::cerr << "Fatal: " << error.what() << "\n";
        return EXIT_FAILURE;
    }
}
