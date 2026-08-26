#include "nordiska/benchmark_metrics.hpp"
#include "nordiska/create_pdf.hpp"
#include "nordiska/json_input_adapter.hpp"
#include "nordiska/libharu_pdf_renderer.hpp"

#include <algorithm>
#include <chrono>
#include <filesystem>
#include <fstream>
#include <iomanip>
#include <iostream>
#include <map>
#include <memory>
#include <optional>
#include <span>
#include <sstream>
#include <stdexcept>
#include <string>
#include <string_view>
#include <vector>

namespace {

using nordiska::benchmark::PhaseMetrics;
using nordiska::Report;
using Clock = std::chrono::steady_clock;

struct Options {
    std::filesystem::path input_directory;
    std::filesystem::path output_directory = "benchmark-output";
    std::string renderer = "haru";
    std::size_t iterations = 3;
    std::size_t warmups = 0;
    std::size_t limit = 0;
    std::size_t sample_count = 3;
    bool delete_output = false;
};

struct LoadedCorpus {
    std::vector<std::filesystem::path> paths;
    std::vector<Report> reports;
    std::size_t transactions{};
};

struct NamedMetrics {
    std::string phase;
    PhaseMetrics metrics;
};

struct RecordedResult {
    std::string renderer;
    std::size_t iteration{};
    NamedMetrics result;
};

std::unique_ptr<nordiska::IPdfRenderer> make_renderer(std::string_view name) {
    if (name == "haru") {
        return std::make_unique<nordiska::LibHaruPdfRenderer>();
    }
    throw std::invalid_argument("this benchmark currently supports only the Haru renderer");
}

Options parse_options(int argc, char* argv[]) {
    if (argc < 2) {
        throw std::invalid_argument(
            "Usage: pdf_generator_benchmark <input-dir> [--iterations N] [--warmups N] "
            "[--limit N] [--sample-count N] [--output-dir DIR] [--delete-output]");
    }
    Options options{.input_directory = argv[1]};
    for (int index = 2; index < argc; ++index) {
        const std::string_view argument = argv[index];
        auto value = [&](std::string_view name) {
            if (++index >= argc) {
                throw std::invalid_argument(std::string(name) + " requires a value");
            }
            return std::string(argv[index]);
        };
        if (argument == "--renderer") {
            options.renderer = value("--renderer");
        } else if (argument == "--iterations") {
            options.iterations = std::stoull(value("--iterations"));
        } else if (argument == "--warmups") {
            options.warmups = std::stoull(value("--warmups"));
        } else if (argument == "--limit") {
            options.limit = std::stoull(value("--limit"));
        } else if (argument == "--sample-count") {
            options.sample_count = std::stoull(value("--sample-count"));
        } else if (argument == "--output-dir") {
            options.output_directory = value("--output-dir");
        } else if (argument == "--delete-output") {
            options.delete_output = true;
        } else {
            throw std::invalid_argument("unknown option: " + std::string(argument));
        }
    }
    if (!std::filesystem::is_directory(options.input_directory)) {
        throw std::runtime_error("input directory does not exist: " +
                                 options.input_directory.string());
    }
    if (options.iterations == 0) {
        throw std::invalid_argument("--iterations must be positive");
    }
    if (options.renderer != "haru") {
        throw std::invalid_argument("this benchmark currently supports only the Haru renderer");
    }
    return options;
}

std::vector<std::filesystem::path> discover(const Options& options) {
    std::vector<std::filesystem::path> paths;
    for (const auto& entry : std::filesystem::directory_iterator(options.input_directory)) {
        if (entry.is_regular_file() && entry.path().extension() == ".json" &&
            entry.path().filename() != "manifest.json") {
            paths.push_back(entry.path());
        }
    }
    std::sort(paths.begin(), paths.end());
    if (options.limit != 0 && paths.size() > options.limit) {
        paths.resize(options.limit);
    }
    if (paths.empty()) {
        throw std::runtime_error("input directory contains no JSON reports");
    }
    return paths;
}

LoadedCorpus load(const std::vector<std::filesystem::path>& paths) {
    nordiska::JsonInputAdapter adapter;
    LoadedCorpus corpus{.paths = paths};
    corpus.reports.reserve(paths.size());
    for (const auto& path : paths) {
        corpus.reports.push_back(adapter.import(path));
        corpus.transactions += corpus.reports.back().transactions.size();
    }
    return corpus;
}

template <typename Function>
PhaseMetrics timed(const LoadedCorpus& corpus, std::optional<std::size_t> output_bytes,
                   Function&& function) {
    const auto started = Clock::now();
    function();
    const double seconds = std::chrono::duration<double>(Clock::now() - started).count();
    return {.seconds = seconds,
            .reports = corpus.reports.size(),
            .transactions = corpus.transactions,
            .output_bytes = output_bytes};
}

std::vector<std::byte> render_to_memory(const Report& report, nordiska::IPdfRenderer& renderer) {
    nordiska::MemoryByteSink sink;
    nordiska::CreatePdf(renderer).execute(report, sink);
    return {sink.bytes().begin(), sink.bytes().end()};
}

std::vector<NamedMetrics> measure(const std::string& renderer_name,
                                  const std::vector<std::filesystem::path>& paths,
                                  const Options& options, std::size_t iteration) {
    LoadedCorpus corpus;
    const auto input_started = Clock::now();
    corpus = load(paths);
    const double input_seconds =
        std::chrono::duration<double>(Clock::now() - input_started).count();
    auto renderer = make_renderer(renderer_name);
    std::vector<std::vector<std::byte>> rendered;
    rendered.reserve(corpus.reports.size());
    for (const auto& report : corpus.reports) {
        rendered.push_back(render_to_memory(report, *renderer));
    }
    std::size_t bytes = 0;
    for (const auto& value : rendered) {
        bytes += value.size();
    }

    std::vector<NamedMetrics> results;
    results.push_back({"input_load_and_parse",
                       {.seconds = input_seconds,
                        .reports = corpus.reports.size(),
                        .transactions = corpus.transactions,
                        .output_bytes = std::nullopt}});
    results.push_back({"memory_render", timed(corpus, bytes, [&] {
                           for (const auto& report : corpus.reports) {
                               nordiska::MemoryByteSink sink;
                               nordiska::CreatePdf(*renderer).execute(report, sink);
                           }
                       })});
    results.push_back({"null_render", timed(corpus, std::nullopt, [&] {
                           for (const auto& report : corpus.reports) {
                               nordiska::NullByteSink sink;
                               nordiska::CreatePdf(*renderer).execute(report, sink);
                           }
                       })});

    const auto persistence_dir =
        options.output_directory / renderer_name / ("iteration-" + std::to_string(iteration));
    std::filesystem::create_directories(persistence_dir);
    results.push_back({"persistence", timed(corpus, bytes, [&] {
                           for (std::size_t index = 0; index < rendered.size(); ++index) {
                               nordiska::FileByteSink sink(
                                   persistence_dir / ("report-" + std::to_string(index) + ".pdf"));
                               sink.write(rendered[index]);
                               sink.finish();
                           }
                       })});
    results.push_back({"end_to_end", timed(corpus, bytes, [&] {
                           nordiska::JsonInputAdapter adapter;
                           for (std::size_t index = 0; index < corpus.paths.size(); ++index) {
                               const Report report = adapter.import(corpus.paths[index]);
                               nordiska::FileByteSink sink(
                                   persistence_dir / ("e2e-" + std::to_string(index) + ".pdf"));
                               nordiska::CreatePdf(*renderer).execute(report, sink);
                           }
                       })});
    if (options.delete_output) {
        std::filesystem::remove_all(persistence_dir);
    }
    return results;
}

void write_csv_row(std::ostream& output, const std::string& renderer, std::size_t iteration,
                   const NamedMetrics& result) {
    const auto& metrics = result.metrics;
    output << renderer << ',' << iteration << ',' << result.phase << ',' << std::fixed
           << std::setprecision(6) << metrics.seconds << ',' << metrics.reports << ','
           << metrics.transactions << ',' << metrics.reports_per_second() << ','
           << metrics.transactions_per_second() << ',';
    if (metrics.output_bytes) {
        output << *metrics.output_bytes;
    } else {
        output << "NA";
    }
    output << '\n';
}

std::string format_bytes(std::optional<std::size_t> bytes) {
    if (!bytes) {
        return "NA";
    }
    std::ostringstream formatted;
    const double value = static_cast<double>(*bytes);
    if (value >= 1024.0 * 1024.0 * 1024.0) {
        formatted << std::fixed << std::setprecision(2) << value / (1024.0 * 1024.0 * 1024.0)
                  << " GiB";
    } else if (value >= 1024.0 * 1024.0) {
        formatted << std::fixed << std::setprecision(2) << value / (1024.0 * 1024.0) << " MiB";
    } else {
        formatted << *bytes << " B";
    }
    return formatted.str();
}

void write_summary(std::ostream& output, const std::vector<RecordedResult>& records) {
    struct Aggregate {
        double seconds{};
        double reports_per_second{};
        double transactions_per_second{};
        std::optional<std::size_t> output_bytes;
        std::size_t count{};
    };
    std::map<std::pair<std::string, std::string>, Aggregate> aggregates;
    for (const auto& record : records) {
        auto& aggregate = aggregates[{record.renderer, record.result.phase}];
        aggregate.seconds += record.result.metrics.seconds;
        aggregate.reports_per_second += record.result.metrics.reports_per_second();
        aggregate.transactions_per_second += record.result.metrics.transactions_per_second();
        aggregate.output_bytes = record.result.metrics.output_bytes;
        ++aggregate.count;
    }

    output << "Renderer | Phase | Avg seconds | Reports/sec | Transactions/sec | Output size\n"
              "--- | --- | ---: | ---: | ---: | ---:\n";
    for (const auto& [key, aggregate] : aggregates) {
        output << key.first << " | " << key.second << " | " << std::fixed << std::setprecision(3)
               << aggregate.seconds / aggregate.count << " | "
               << aggregate.reports_per_second / aggregate.count << " | "
               << aggregate.transactions_per_second / aggregate.count << " | "
               << format_bytes(aggregate.output_bytes) << "\n";
    }
}

void write_human_report(const std::filesystem::path& path, const Options& options,
                        const std::filesystem::path& run_directory,
                        const std::vector<RecordedResult>& records, std::size_t report_count,
                        std::size_t transaction_count) {
    std::ofstream output(path);
    if (!output) {
        throw std::runtime_error("could not create benchmark report: " + path.string());
    }
    output << "# Nordiska native PDF benchmark\n\n"
              "- Input corpus: `"
           << options.input_directory.string()
           << "`\n"
              "- Reports: "
           << report_count
           << "\n"
              "- Transactions: "
           << transaction_count
           << "\n"
              "- Measured iterations: "
           << options.iterations
           << "\n"
              "- Warmup iterations: "
           << options.warmups
           << "\n"
              "- Output directory: `"
           << run_directory.string()
           << "`\n\n"
              "The timings are wall-clock durations. Throughput is calculated from the full corpus "
              "count."
              " Input loading includes file reading and JSON parsing. Persistence writes "
              "already-rendered"
              " bytes through `FileByteSink`; end-to-end includes loading, rendering, and "
              "persistence.\n\n"
              "## Summary\n\n";
    write_summary(output, records);
    output << "\nRaw per-iteration measurements are in [`results.csv`](results.csv).\n";
}

std::filesystem::path write_samples(const std::vector<std::filesystem::path>& paths,
                                    const Options& options,
                                    const std::filesystem::path& run_directory) {
    const auto sample_directory = run_directory / "samples" / "haru";
    const std::size_t count = std::min(options.sample_count, paths.size());
    if (count == 0) {
        return sample_directory;
    }
    std::filesystem::create_directories(sample_directory);
    nordiska::JsonInputAdapter adapter;
    auto renderer = make_renderer("haru");
    for (std::size_t index = 0; index < count; ++index) {
        const Report report = adapter.import(paths[index]);
        nordiska::FileByteSink sink(sample_directory /
                                    ("sample-" + std::to_string(index + 1) + ".pdf"));
        nordiska::CreatePdf(*renderer).execute(report, sink);
    }
    return sample_directory;
}

void print_execution_plan(const Options& options, const std::vector<std::string>& renderers,
                          std::size_t report_count) {
    const std::size_t runs_per_renderer = options.warmups + options.iterations;
    const std::size_t total_runs = renderers.size() * runs_per_renderer;
    const std::size_t measured_rows = renderers.size() * options.iterations * 5;
    const std::size_t all_phase_executions = total_runs * 5;
    const std::size_t corpus_loads = total_runs * 2;
    const std::size_t pdf_renders = total_runs * report_count * 4;
    const std::size_t pdf_writes = total_runs * report_count * 2;

    std::cout << "Nordiska native PDF benchmark plan\n"
              << "  Renderers: " << renderers.size() << " (";
    for (std::size_t index = 0; index < renderers.size(); ++index) {
        if (index != 0) {
            std::cout << ", ";
        }
        std::cout << renderers[index];
    }
    std::cout << ")\n"
              << "  Corpus: " << report_count << " reports\n"
              << "  Warmups: " << options.warmups << " per renderer\n"
              << "  Measured iterations: " << options.iterations << " per renderer\n"
              << "  Total benchmark passes: " << total_runs << "\n"
              << "  Measured result rows: " << measured_rows << "\n"
              << "  Phase executions including warmups: " << all_phase_executions << "\n"
              << "  Corpus loads including end-to-end reloads: " << corpus_loads << "\n"
              << "  PDF renders including preparation: " << pdf_renders << "\n"
              << "  PDF file writes including end-to-end: " << pdf_writes << "\n\n";
}

} // namespace

int main(int argc, char* argv[]) {
    try {
        Options options = parse_options(argc, argv);
        const auto paths = discover(options);
        std::filesystem::create_directories(options.output_directory);
        options.output_directory /=
            "run-" + std::to_string(std::chrono::duration_cast<std::chrono::nanoseconds>(
                                        Clock::now().time_since_epoch())
                                        .count());
        std::filesystem::create_directories(options.output_directory);
        const auto csv_path = options.output_directory / "results.csv";
        const auto report_path = options.output_directory / "report.md";
        std::ofstream csv(csv_path);
        if (!csv) {
            throw std::runtime_error("could not create benchmark CSV: " + csv_path.string());
        }
        csv << "renderer,iteration,phase,seconds,reports,transactions,reports_per_second,"
               "transactions_per_second,output_bytes\n";
        const std::vector<std::string> renderers{"haru"};
        print_execution_plan(options, renderers, paths.size());
        std::vector<RecordedResult> records;
        for (const auto& renderer : renderers) {
            for (std::size_t warmup = 0; warmup < options.warmups; ++warmup) {
                (void)measure(renderer, paths, options, warmup);
            }
            for (std::size_t iteration = 0; iteration < options.iterations; ++iteration) {
                for (const auto& result : measure(renderer, paths, options, iteration)) {
                    write_csv_row(csv, renderer, iteration, result);
                    records.push_back({renderer, iteration, result});
                }
            }
        }
        csv.close();
        if (records.empty()) {
            throw std::runtime_error("benchmark produced no measured results");
        }
        const std::size_t report_count = records.front().result.metrics.reports;
        const std::size_t transaction_count = records.front().result.metrics.transactions;
        const auto sample_directory = write_samples(paths, options, options.output_directory);
        write_human_report(report_path, options, options.output_directory, records, report_count,
                           transaction_count);
        std::cout << "\nNordiska native PDF benchmark\n"
                  << "Corpus: " << report_count << " reports, " << transaction_count
                  << " transactions\n\n";
        write_summary(std::cout, records);
        std::cout << "\nCSV: " << csv_path << "\nReport: " << report_path << '\n';
        if (options.sample_count != 0) {
            std::cout << "Samples: " << sample_directory << " ("
                      << std::min(options.sample_count, paths.size()) << " PDFs)\n";
        }
        std::cerr << "Benchmark output directory: " << options.output_directory << '\n';
        if (options.delete_output) {
            std::filesystem::remove_all(options.output_directory);
        }
        return 0;
    } catch (const std::exception& error) {
        std::cerr << "PDF benchmark failed: " << error.what() << '\n';
        return 1;
    }
}
