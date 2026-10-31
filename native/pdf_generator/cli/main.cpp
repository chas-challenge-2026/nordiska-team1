#include "nordiska/application/pdf_generator.hpp"
#include "output.hpp"

#include <charconv>
#include <chrono>
#include <cstdlib>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <iterator>
#include <new>
#include <string>
#include <strings.h>
#include <unistd.h>

namespace {
using namespace nordiska;
struct Options {
    std::filesystem::path input;
    std::filesystem::path output{"."};
    GeneratorConfig generator;
    bool quiet{};
    bool verbose{};
    bool json_summary{};
    bool help{};
    bool version{};
};
Diagnostic cli_error(DiagnosticCode code, DiagnosticField field = DiagnosticField::None) {
    return {code, DiagnosticStage::Cli, {.field = field}};
}
std::expected<PdfEngineKind, Diagnostic> parse_engine(const char* value) {
    if (strcasecmp(value, "native") == 0 || strcasecmp(value, "fast") == 0) {
        return PdfEngineKind::Native;
    }
    if (strcasecmp(value, "haru") == 0 || strcasecmp(value, "libharu") == 0) {
        return PdfEngineKind::Libharu;
    }
    if (strcasecmp(value, "cairo") == 0) {
        return PdfEngineKind::Cairo;
    }
    return std::unexpected(cli_error(DiagnosticCode::InvalidEngine, DiagnosticField::Engine));
}
std::expected<Options, Diagnostic> parse_cli(int argc, char* argv[]) {
    Options options;
    if (const char* value = std::getenv("NORDISKA_PDF_ENGINE"); value && *value) {
        auto engine = parse_engine(value);
        if (!engine) {
            return std::unexpected(engine.error());
        }
        options.generator.engine = *engine;
    }
    for (int index = 1; index < argc; ++index) {
        const std::string_view argument = argv[index];
        if (argument == "-h" || argument == "--help") {
            options.help = true;
            continue;
        }
        if (argument == "-V" || argument == "--version") {
            options.version = true;
            continue;
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
        if (argument == "--signing") {
            options.generator.enable_signing = true;
            continue;
        }
        if (argument == "--no-signing") {
            options.generator.enable_signing = false;
            continue;
        }
        if (argument == "--no-compression") {
            options.generator.compression = false;
            continue;
        }
        DiagnosticField field = DiagnosticField::None;
        if (argument == "-i" || argument == "--input") {
            field = DiagnosticField::Input;
        } else if (argument == "-o" || argument == "--output") {
            field = DiagnosticField::Output;
        } else if (argument == "-r" || argument == "--renderer") {
            field = DiagnosticField::Engine;
        } else if (argument == "-e" || argument == "--ingestor") {
            field = DiagnosticField::Ingestor;
        } else if (argument == "--compression") {
            field = DiagnosticField::Compression;
        } else if (argument == "--signature-capacity") {
            field = DiagnosticField::SignatureCapacity;
        }
        if (field != DiagnosticField::None) {
            if (++index >= argc) {
                return std::unexpected(cli_error(DiagnosticCode::MissingOptionValue, field));
            }
            const char* value = argv[index];
            const std::string_view text = value;
            switch (field) {
            case DiagnosticField::Input:
                options.input = value;
                break;
            case DiagnosticField::Output:
                options.output = value;
                break;
            case DiagnosticField::Engine: {
                auto engine = parse_engine(value);
                if (!engine) {
                    return std::unexpected(engine.error());
                }
                options.generator.engine = *engine;
                break;
            }
            case DiagnosticField::Ingestor:
                if (strcasecmp(value, "simdjson") == 0 || strcasecmp(value, "simd") == 0) {
                    options.generator.ingestor = JsonIngestorKind::Simdjson;
                } else if (strcasecmp(value, "nlohmann") == 0) {
                    options.generator.ingestor = JsonIngestorKind::Nlohmann;
                } else {
                    return std::unexpected(cli_error(DiagnosticCode::InvalidIngestor, field));
                }
                break;
            case DiagnosticField::Compression:
                if (text == "true" || text == "1" || text == "yes") {
                    options.generator.compression = true;
                } else if (text == "false" || text == "0" || text == "no") {
                    options.generator.compression = false;
                } else {
                    return std::unexpected(cli_error(DiagnosticCode::InvalidBoolean, field));
                }
                break;
            case DiagnosticField::SignatureCapacity: {
                auto& capacity = options.generator.signature_contents_capacity;
                const auto [end, error] = std::from_chars(text.data(), text.data() + text.size(), capacity);
                if (error != std::errc{} || end != text.data() + text.size() || capacity == 0 || capacity % 2 != 0) {
                    return std::unexpected(cli_error(DiagnosticCode::InvalidSignatureCapacity, field));
                }
                break;
            }
            default:
                break;
            }
            continue;
        }
        if ((argument.starts_with('-') && argument != "-") || !options.input.empty()) {
            return std::unexpected(cli_error(DiagnosticCode::InvalidOption));
        }
        options.input = argument;
    }
    if (options.input.empty() && !isatty(STDIN_FILENO)) {
        options.input = "-";
    }
    if (options.input.empty() && !options.help && !options.version) {
        return std::unexpected(cli_error(DiagnosticCode::InputRequired, DiagnosticField::Input));
    }
    return options;
}
std::expected<std::vector<uint8_t>, Diagnostic> read_input(const std::filesystem::path& path) {
    std::ifstream file;
    if (path != "-") {
        file.open(path, std::ios::binary);
        if (!file) {
            return std::unexpected(cli_error(DiagnosticCode::InputUnavailable, DiagnosticField::Input));
        }
    }
    std::istream& input = path == "-" ? std::cin : file;
    std::vector<uint8_t> bytes{std::istreambuf_iterator<char>(input), std::istreambuf_iterator<char>()};
    if (input.bad()) {
        return std::unexpected(cli_error(DiagnosticCode::InputUnavailable, DiagnosticField::Input));
    }
    return bytes;
}
int run(int argc, char* argv[]) {
    const auto parsed = parse_cli(argc, argv);
    if (!parsed) {
        cli::write_error(parsed.error());
        return 1;
    }
    const auto& options = *parsed;
    if (options.help) {
        cli::write_usage();
        return 0;
    }
    if (options.version) {
        cli::write_version();
        return 0;
    }
    using Clock = std::chrono::steady_clock;
    const auto start = Clock::now();
    const auto payload = read_input(options.input);
    if (!payload) {
        cli::write_error(payload.error(), options.json_summary);
        return 2;
    }
    const PdfGenerator generator(options.generator);
    const auto generation_start = Clock::now();
    const auto generated = generator.generate(*payload);
    const auto generation_end = Clock::now();
    if (!generated) {
        const auto diagnostic = generated.error().diagnostic();
        cli::write_error(diagnostic, options.json_summary);
        if (diagnostic.stage == DiagnosticStage::Ingestion) {
            return 3;
        }
        if (diagnostic.stage == DiagnosticStage::Signing) {
            return 5;
        }
        return 4;
    }
    cli::Statistics statistics{.engine = options.generator.engine, .ingestor = options.generator.ingestor};
    const bool single_file = generated->documents.size() == 1 && options.output.extension() == ".pdf";
    const auto directory = single_file ? options.output.parent_path() : options.output;
    if (!directory.empty()) {
        std::filesystem::create_directories(directory);
    }
    for (size_t index = 0; index < generated->documents.size(); ++index) {
        const auto& document = generated->documents[index];
        const auto path = single_file ? options.output : options.output / (document.document_id + ".pdf");
        std::ofstream output(path, std::ios::binary);
        output.write(reinterpret_cast<const char*>(document.pdf_bytes.data()),
                     static_cast<std::streamsize>(document.pdf_bytes.size()));
        output.close();
        if (!output) {
            cli::write_error({DiagnosticCode::OutputUnavailable,
                              DiagnosticStage::Cli,
                              {.field = DiagnosticField::Output, .document_index = index}},
                             options.json_summary);
            return 2;
        }
        statistics.documents.push_back({index, document.pdf_bytes.size()});
        if (!options.quiet && !options.json_summary) {
            cli::write_document(statistics.documents.back());
        }
    }
    statistics.generation_ms = std::chrono::duration<double, std::milli>(generation_end - generation_start).count();
    statistics.total_ms = std::chrono::duration<double, std::milli>(Clock::now() - start).count();
    if (options.json_summary || !options.quiet) {
        cli::write_statistics(statistics, options.json_summary, options.verbose);
    }
    return 0;
}
} // namespace
int main(int argc, char* argv[]) {
    try {
        return run(argc, argv);
    } catch (const std::bad_alloc&) {
        nordiska::cli::write_error({nordiska::DiagnosticCode::OutOfMemory});
    } catch (...) {
        nordiska::cli::write_error({nordiska::DiagnosticCode::UnexpectedFailure});
    }
    return 4;
}
