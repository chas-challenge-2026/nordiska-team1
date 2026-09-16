#include "nordiska/application/pdf_generator.hpp"
#include "nordiska/domain/generated_pdfs.hpp"

#include <chrono>
#include <cstdlib>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <nlohmann/json.hpp>
#include <string>
#include <string_view>
#include <unistd.h>
#include <vector>

namespace {

constexpr std::string_view kVersion = "0.2.0";

namespace ExitCode {
constexpr int Success = 0;
constexpr int CliUsage = 1;
constexpr int IoError = 2;
constexpr int IngestError = 3;
constexpr int RenderError = 4;
} // namespace ExitCode

struct CliOptions {
    std::filesystem::path input_path;
    std::filesystem::path output_path = ".";
    std::string renderer = "haru";
    std::string ingestor = "nlohmann";
    bool quiet = false;
    bool verbose = false;
    bool json_summary = false;
    bool print_version = false;
    bool print_help = false;
};

void print_usage(std::string_view program_name) {
    std::cout << "Nordiska PDF Generator CLI — Standalone Native Batch Worker (v" << kVersion << ")\n\n"
              << "Usage:\n"
              << "  " << program_name << " [OPTIONS] [INPUT_JSON]\n\n"
              << "Arguments:\n"
              << "  [INPUT_JSON]               Path to input JSON file, or '-' for stdin.\n"
              << "                             If omitted and stdin is piped, reads from stdin.\n\n"
              << "Options:\n"
              << "  -i, --input <path>         Path to input JSON file (or '-' for stdin)\n"
              << "  -o, --output <path>        Output directory or target file (default: current directory)\n"
              << "  -r, --renderer <engine>    Rendering engine: 'haru' (default) or 'cairo'\n"
              << "  -e, --ingestor <engine>    JSON ingestor: 'nlohmann' (default) or 'simdjson'\n"
              << "  -q, --quiet                Quiet mode (suppress progress messages, only report errors)\n"
              << "  -v, --verbose              Verbose mode (print detailed breakdown and elapsed timing)\n"
              << "      --json-summary         Output machine-readable JSON execution summary to stdout\n"
              << "  -V, --version              Print version information and exit\n"
              << "  -h, --help                 Print this help message and exit\n\n"
              << "Exit Codes:\n"
              << "  0  Success\n"
              << "  1  Invalid command line options or syntax\n"
              << "  2  I/O error (file read or write failure)\n"
              << "  3  JSON ingestion or validation error\n"
              << "  4  Document rendering or internal error\n";
}

void print_version_info() {
    std::cout << "nordiska-pdf " << kVersion << " (Linux x86_64, C++23)\n"
              << "Engines: libharu, cairo | Ingestors: nlohmann-json, simdjson\n";
}

CliOptions parse_cli(int argc, char* argv[]) {
    CliOptions options;

    for (int index = 1; index < argc; ++index) {
        const std::string_view argument = argv[index];

        if (argument == "-h" || argument == "--help") {
            options.print_help = true;
            return options;
        }
        if (argument == "-V" || argument == "--version") {
            options.print_version = true;
            return options;
        }
        if (argument == "-q" || argument == "--quiet") {
            options.quiet = true;
            continue;
        }
        if (argument == "-v" || argument == "--verbose") {
            options.verbose = true;
            continue;
        }
        if (argument == "--json-summary") {
            options.json_summary = true;
            continue;
        }
        if (argument == "-i" || argument == "--input") {
            if (++index >= argc) {
                std::cerr << "Error: " << argument << " requires a file path\n";
                std::exit(ExitCode::CliUsage);
            }
            options.input_path = argv[index];
            continue;
        }
        if (argument == "-o" || argument == "--output") {
            if (++index >= argc) {
                std::cerr << "Error: " << argument << " requires a path value\n";
                std::exit(ExitCode::CliUsage);
            }
            options.output_path = argv[index];
            continue;
        }
        if (argument == "-r" || argument == "--renderer") {
            if (++index >= argc) {
                std::cerr << "Error: " << argument << " requires a renderer name\n";
                std::exit(ExitCode::CliUsage);
            }
            options.renderer = argv[index];
            continue;
        }
        if (argument == "-e" || argument == "--ingestor") {
            if (++index >= argc) {
                std::cerr << "Error: " << argument << " requires an ingestor name\n";
                std::exit(ExitCode::CliUsage);
            }
            options.ingestor = argv[index];
            continue;
        }
        if (argument.starts_with("-") && argument != "-") {
            std::cerr << "Error: unknown option '" << argument << "'\n\n";
            print_usage(argv[0]);
            std::exit(ExitCode::CliUsage);
        }

        // Positional argument for input file
        if (!options.input_path.empty()) {
            std::cerr << "Error: unexpected extra argument '" << argument << "'\n\n";
            print_usage(argv[0]);
            std::exit(ExitCode::CliUsage);
        }
        options.input_path = argv[index];
    }

    // Check if input should be read from piped stdin
    if (options.input_path.empty()) {
        if (!isatty(fileno(stdin))) {
            options.input_path = "-";
        } else {
            std::cerr << "Error: input JSON file is required\n\n";
            print_usage(argv[0]);
            std::exit(ExitCode::CliUsage);
        }
    }

    return options;
}

std::expected<std::vector<uint8_t>, std::string> read_input_payload(const std::filesystem::path& input_path) {
    std::vector<uint8_t> payload;

    if (input_path == "-") {
        char buffer[8192];
        while (std::cin.read(buffer, sizeof(buffer)) || std::cin.gcount() > 0) {
            const auto count = std::cin.gcount();
            payload.insert(payload.end(), buffer, buffer + count);
        }
        if (payload.empty()) {
            return std::unexpected("standard input was empty");
        }
        return payload;
    }

    if (!std::filesystem::exists(input_path)) {
        return std::unexpected("input file does not exist: " + input_path.string());
    }

    std::ifstream file(input_path, std::ios::binary);
    if (!file) {
        return std::unexpected("failed to open input file: " + input_path.string());
    }

    file.seekg(0, std::ios::end);
    const auto file_size = file.tellg();
    file.seekg(0, std::ios::beg);

    if (file_size > 0) {
        payload.resize(static_cast<std::size_t>(file_size));
        if (!file.read(reinterpret_cast<char*>(payload.data()), file_size)) {
            return std::unexpected("failed to read input file: " + input_path.string());
        }
    }

    return payload;
}

} // namespace

int main(int argc, char* argv[]) {
    try {
        const CliOptions options = parse_cli(argc, argv);

        if (options.print_help) {
            print_usage(argv[0]);
            return ExitCode::Success;
        }
        if (options.print_version) {
            print_version_info();
            return ExitCode::Success;
        }

        const auto start_time = std::chrono::steady_clock::now();

        // 1. Read input payload
        const auto payload_result = read_input_payload(options.input_path);
        if (!payload_result) {
            std::cerr << "Error: " << payload_result.error() << "\n";
            return ExitCode::IoError;
        }
        const auto& payload = *payload_result;

        // 2. Configure generator
        nordiska::GeneratorConfig config;
        if (options.renderer == "haru") {
            config.engine = nordiska::PdfEngineKind::Libharu;
        } else if (options.renderer == "cairo") {
            config.engine = nordiska::PdfEngineKind::Cairo;
        } else {
            std::cerr << "Error: unsupported renderer engine '" << options.renderer
                      << "'. Supported: 'haru', 'cairo'\n";
            return ExitCode::CliUsage;
        }

        if (options.ingestor == "nlohmann") {
            config.ingestor = nordiska::JsonIngestorKind::Nlohmann;
        } else if (options.ingestor == "simdjson") {
            config.ingestor = nordiska::JsonIngestorKind::Simdjson;
        } else {
            std::cerr << "Error: unsupported JSON ingestor '" << options.ingestor
                      << "'. Supported: 'nlohmann', 'simdjson'\n";
            return ExitCode::CliUsage;
        }

        // 3. Execute batch generation
        const nordiska::PdfGenerator generator(config);
        const auto generate_start = std::chrono::steady_clock::now();
        const auto result = generator.generate(payload);
        const auto generate_end = std::chrono::steady_clock::now();

        if (!result) {
            if (options.json_summary) {
                nlohmann::json summary = {
                    {"status", "error"},
                    {"error_message", result.error().message},
                };
                std::cout << summary.dump(2) << "\n";
            }
            std::cerr << "Generation failed: " << result.error().message << "\n";

            switch (result.error().kind) {
            case nordiska::GeneratorErrorKind::InvalidInput:
                return ExitCode::IngestError;
            case nordiska::GeneratorErrorKind::InvalidArgument:
                return ExitCode::CliUsage;
            case nordiska::GeneratorErrorKind::ResourceLimitExceeded:
            case nordiska::GeneratorErrorKind::InternalError:
            default:
                return ExitCode::RenderError;
            }
        }

        const auto& generated = *result;

        // 4. Determine output targets and write files
        std::filesystem::path output_directory = ".";
        bool single_file_mode = false;
        std::filesystem::path single_output_file;

        if (!options.output_path.empty()) {
            if (generated.documents.size() == 1 && options.output_path.extension() == ".pdf") {
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

        struct WrittenDocInfo {
            std::string document_id;
            std::filesystem::path file_path;
            std::size_t bytes{0};
        };
        std::vector<WrittenDocInfo> written_docs;
        written_docs.reserve(generated.documents.size());

        for (std::size_t index = 0; index < generated.documents.size(); ++index) {
            const auto& doc = generated.documents[index];
            std::filesystem::path target_path;
            if (single_file_mode) {
                target_path = single_output_file;
            } else {
                const std::string filename = doc.document_id.empty() ? ("document-" + std::to_string(index) + ".pdf")
                                                                     : (doc.document_id + ".pdf");
                target_path = output_directory / filename;
            }

            std::ofstream out(target_path, std::ios::binary);
            if (!out) {
                std::cerr << "Error: failed to open output file for writing: " << target_path << "\n";
                return ExitCode::IoError;
            }
            out.write(reinterpret_cast<const char*>(doc.pdf_bytes.data()),
                      static_cast<std::streamsize>(doc.pdf_bytes.size()));
            if (!out) {
                std::cerr << "Error: failed while writing PDF bytes to: " << target_path << "\n";
                return ExitCode::IoError;
            }

            written_docs.push_back({
                .document_id = doc.document_id,
                .file_path = target_path,
                .bytes = doc.pdf_bytes.size(),
            });

            if (!options.quiet && !options.json_summary) {
                std::cout << "Wrote " << target_path.string() << " (" << doc.pdf_bytes.size() << " bytes)\n";
            }
        }

        const auto end_time = std::chrono::steady_clock::now();
        const auto total_ms =
            std::chrono::duration_cast<std::chrono::duration<double, std::milli>>(end_time - start_time).count();
        const auto gen_ms =
            std::chrono::duration_cast<std::chrono::duration<double, std::milli>>(generate_end - generate_start)
                .count();

        // 5. Output summary
        if (options.json_summary) {
            nlohmann::json doc_array = nlohmann::json::array();
            for (const auto& item : written_docs) {
                doc_array.push_back({
                    {"document_id", item.document_id},
                    {"file_path", item.file_path.string()},
                    {"bytes", item.bytes},
                });
            }

            nlohmann::json summary = {
                {"status", "success"},
                {"customer_id", generated.customer_id},
                {"document_count", generated.documents.size()},
                {"renderer", options.renderer},
                {"ingestor", options.ingestor},
                {"generation_ms", gen_ms},
                {"total_ms", total_ms},
                {"documents", doc_array},
            };
            std::cout << summary.dump(2) << "\n";
        } else if (!options.quiet) {
            if (options.verbose) {
                std::cout << "\nBatch generation summary:\n"
                          << "  Customer ID:     " << generated.customer_id << "\n"
                          << "  Documents:       " << generated.documents.size() << "\n"
                          << "  Renderer engine: " << options.renderer << "\n"
                          << "  JSON ingestor:   " << options.ingestor << "\n"
                          << "  Generation time: " << gen_ms << " ms\n"
                          << "  Total wall time: " << total_ms << " ms\n";
            } else {
                std::cout << "Successfully generated " << generated.documents.size() << " document(s) in " << total_ms
                          << " ms\n";
            }
        }

        return ExitCode::Success;
    } catch (const std::exception& error) {
        std::cerr << "Fatal error: " << error.what() << "\n";
        return ExitCode::RenderError;
    }
}
