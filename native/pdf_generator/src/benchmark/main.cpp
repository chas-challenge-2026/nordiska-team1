#include "nordiska/application/pdf_generator.hpp"
#include "nordiska/diagnostics/benchmark_metrics.hpp"

#include <chrono>
#include <filesystem>
#include <fstream>
#include <iomanip>
#include <iostream>
#include <stdexcept>
#include <string>
#include <string_view>
#include <vector>

namespace {

using Clock = std::chrono::steady_clock;

struct Options {
    std::filesystem::path input_path;
    std::string renderer = "haru";
    std::string ingestor = "nlohmann";
    std::size_t iterations = 5;
    std::size_t warmups = 1;
};

Options parse_options(int argc, char* argv[]) {
    if (argc < 2) {
        std::cout << "Usage: pdf_generator_benchmark <input-file-or-dir> [options]\n\n"
                  << "Options:\n"
                  << "  --renderer <haru|cairo>     Rendering engine (default: haru)\n"
                  << "  --ingestor <nlohmann|simd>  JSON ingestor (default: nlohmann)\n"
                  << "  --iterations <N>            Measurement iterations (default: 5)\n"
                  << "  --warmups <N>               Warmup iterations (default: 1)\n";
        std::exit(EXIT_FAILURE);
    }

    Options options{.input_path = argv[1]};
    for (int index = 2; index < argc; ++index) {
        const std::string_view arg = argv[index];
        auto next_value = [&](std::string_view opt_name) {
            if (++index >= argc) {
                throw std::invalid_argument(std::string(opt_name) + " requires a value");
            }
            return std::string(argv[index]);
        };

        if (arg == "--renderer") {
            options.renderer = next_value("--renderer");
        } else if (arg == "--ingestor") {
            options.ingestor = next_value("--ingestor");
        } else if (arg == "--iterations") {
            options.iterations = std::stoull(next_value("--iterations"));
        } else if (arg == "--warmups") {
            options.warmups = std::stoull(next_value("--warmups"));
        } else {
            throw std::invalid_argument("unknown option: " + std::string(arg));
        }
    }
    return options;
}

std::vector<std::vector<uint8_t>> load_payloads(const std::filesystem::path& path) {
    std::vector<std::filesystem::path> files;
    if (std::filesystem::is_directory(path)) {
        for (const auto& entry : std::filesystem::directory_iterator(path)) {
            if (entry.is_regular_file() && entry.path().extension() == ".json") {
                files.push_back(entry.path());
            }
        }
    } else if (std::filesystem::is_regular_file(path)) {
        files.push_back(path);
    } else {
        throw std::runtime_error("Path does not exist: " + path.string());
    }

    std::vector<std::vector<uint8_t>> payloads;
    payloads.reserve(files.size());
    for (const auto& file_path : files) {
        std::ifstream stream(file_path, std::ios::binary);
        if (!stream) {
            throw std::runtime_error("Failed to open file: " + file_path.string());
        }
        payloads.emplace_back((std::istreambuf_iterator<char>(stream)), std::istreambuf_iterator<char>());
    }
    return payloads;
}

} // namespace

int main(int argc, char* argv[]) {
    try {
        const Options options = parse_options(argc, argv);
        const auto payloads = load_payloads(options.input_path);
        if (payloads.empty()) {
            std::cerr << "No JSON files found at " << options.input_path << "\n";
            return EXIT_FAILURE;
        }

        nordiska::GeneratorConfig config;
        if (options.renderer == "haru") {
            config.engine = nordiska::PdfEngineKind::Libharu;
        } else if (options.renderer == "cairo") {
            config.engine = nordiska::PdfEngineKind::Cairo;
        } else {
            std::cerr << "Unsupported renderer: " << options.renderer << "\n";
            return EXIT_FAILURE;
        }

        if (options.ingestor == "nlohmann") {
            config.ingestor = nordiska::JsonIngestorKind::Nlohmann;
        } else if (options.ingestor == "simdjson" || options.ingestor == "simd") {
            config.ingestor = nordiska::JsonIngestorKind::Simdjson;
        } else {
            std::cerr << "Unsupported ingestor: " << options.ingestor << "\n";
            return EXIT_FAILURE;
        }

        const nordiska::PdfGenerator generator(config);

        // Warmup
        for (std::size_t w = 0; w < options.warmups; ++w) {
            for (const auto& payload : payloads) {
                const auto res = generator.generate(payload);
                if (!res) {
                    std::cerr << "Warmup error: " << res.error().message << "\n";
                    return EXIT_FAILURE;
                }
            }
        }

        std::cout << "Benchmarking " << payloads.size() << " payload(s) with renderer=" << options.renderer
                  << ", ingestor=" << options.ingestor << " (" << options.iterations << " iterations)...\n";

        double total_seconds = 0.0;
        std::size_t total_docs = 0;
        std::size_t total_bytes = 0;

        for (std::size_t iter = 0; iter < options.iterations; ++iter) {
            const auto start = Clock::now();
            std::size_t iter_docs = 0;
            std::size_t iter_bytes = 0;

            for (const auto& payload : payloads) {
                const auto res = generator.generate(payload);
                if (!res) {
                    std::cerr << "Benchmark error: " << res.error().message << "\n";
                    return EXIT_FAILURE;
                }
                iter_docs += res->documents.size();
                for (const auto& doc : res->documents) {
                    iter_bytes += doc.pdf_bytes.size();
                }
            }
            const auto end = Clock::now();
            const double iter_sec = std::chrono::duration<double>(end - start).count();

            total_seconds += iter_sec;
            total_docs += iter_docs;
            total_bytes += iter_bytes;

            std::cout << "  Iter " << (iter + 1) << ": " << std::fixed << std::setprecision(2) << (iter_sec * 1000.0)
                      << " ms (" << iter_docs << " docs, " << (iter_docs / iter_sec) << " docs/sec)\n";
        }

        const double avg_sec = total_seconds / static_cast<double>(options.iterations);
        const double avg_docs = static_cast<double>(total_docs) / static_cast<double>(options.iterations);
        const double avg_throughput = avg_docs / avg_sec;
        const double avg_kb = (static_cast<double>(total_bytes) / static_cast<double>(options.iterations)) / 1024.0;

        std::cout << "\nResults:\n"
                  << "  Average time:        " << std::fixed << std::setprecision(2) << (avg_sec * 1000.0) << " ms\n"
                  << "  Average documents:   " << avg_docs << "\n"
                  << "  Document throughput: " << std::setprecision(1) << avg_throughput << " docs/sec\n"
                  << "  Output generated:    " << std::setprecision(1) << avg_kb << " KB/batch\n";

        return EXIT_SUCCESS;
    } catch (const std::exception& error) {
        std::cerr << "Fatal error: " << error.what() << "\n";
        return EXIT_FAILURE;
    }
}
