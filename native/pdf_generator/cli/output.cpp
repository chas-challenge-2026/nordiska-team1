#include "output.hpp"

#include <iostream>
#include <nlohmann/json.hpp>

namespace nordiska::cli {
namespace {
std::string_view engine_name(PdfEngineKind engine) {
    switch (engine) {
    case PdfEngineKind::Libharu:
        return "haru";
    case PdfEngineKind::Native:
        return "native";
    case PdfEngineKind::Cairo:
        return "cairo";
    }
    return "unknown";
}
std::string_view ingestor_name(JsonIngestorKind ingestor) {
    switch (ingestor) {
    case JsonIngestorKind::Simdjson:
        return "simdjson";
    case JsonIngestorKind::Nlohmann:
        return "nlohmann";
    }
    return "unknown";
}
} // namespace
void write_error(Diagnostic diagnostic, bool json_summary) {
    const auto text = format_diagnostic(diagnostic);
    if (json_summary) {
        std::cout << nlohmann::json{{"status", "error"}, {"error_message", text.view()}}.dump(2) << '\n';
    }
    std::cerr << text.view() << '\n';
}
void write_document(DocumentStatistics document) {
    std::cout << "Wrote document " << document.index << " (" << document.bytes << " bytes)\n";
}
void write_statistics(const Statistics& statistics, bool json_summary, bool verbose) {
    if (json_summary) {
        auto documents = nlohmann::json::array();
        for (const auto& document : statistics.documents) {
            documents.push_back({{"document_index", document.index}, {"bytes", document.bytes}});
        }
        std::cout << nlohmann::json{{"status", "success"},
                                    {"document_count", statistics.documents.size()},
                                    {"renderer", engine_name(statistics.engine)},
                                    {"ingestor", ingestor_name(statistics.ingestor)},
                                    {"generation_ms", statistics.generation_ms},
                                    {"total_ms", statistics.total_ms},
                                    {"documents", documents}}
                         .dump(2)
                  << '\n';
    } else {
        std::cout << "Successfully generated " << statistics.documents.size() << " document(s) in "
                  << statistics.total_ms << " ms\n";
        if (verbose) {
            std::cout << "Renderer: " << engine_name(statistics.engine)
                      << "\nIngestor: " << ingestor_name(statistics.ingestor)
                      << "\nGeneration time: " << statistics.generation_ms << " ms\n";
        }
    }
}
void write_usage() {
    std::cout << "Usage: pdf_generator [OPTIONS] [INPUT_JSON]\n"
                 "Input: JSON file or '-' for stdin; piped stdin is the default.\n"
                 "  -i, --input <path>\n  -o, --output <path>\n"
                 "  -r, --renderer <haru|native|cairo>\n  -e, --ingestor <simdjson|nlohmann>\n"
                 "  --compression <true|false>\n  --no-compression\n"
                 "  --signing\n  --no-signing\n  --signature-capacity <even hex character count>\n"
                 "  -q, --quiet\n  -v, --verbose\n  --json-summary\n  -h, --help\n  -V, --version\n"
                 "Exit codes: 0 success, 1 usage, 2 I/O, 3 input, 4 internal, 5 signing.\n";
}
void write_version() {
    std::cout << "nordiska-pdf 0.2.0 (C++23)\n";
}
} // namespace nordiska::cli
