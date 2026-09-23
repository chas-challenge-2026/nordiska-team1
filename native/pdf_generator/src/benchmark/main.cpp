#include "nordiska/application/pdf_generator.hpp"
#include "nordiska/c_api/pdf_generator_c_api.h"
#include "nordiska/diagnostics/benchmark_metrics.hpp"
#include "nordiska/signing/pdf_signer.hpp"

#include <atomic>
#include <chrono>
#include <filesystem>
#include <fstream>
#include <iomanip>
#include <iostream>
#include <mutex>
#include <nlohmann/json.hpp>
#include <stdexcept>
#include <string>
#include <string_view>
#include <sys/resource.h>
#include <thread>
#include <unistd.h>
#include <vector>

namespace {

// Stub signer: returns a fixed 8 192-char hex string (same size as his current stub) without
// requiring SoftHSM or any real crypto. Used by --signing in the benchmark to measure our
// full pipeline cost (slot append, both SHA-256 hashes, hex insertion) independent of the
// real signer's environment and crypto latency.
class StubPdfSigner final : public nordiska::PdfSigner {
  public:
    [[nodiscard]] std::expected<std::string, nordiska::SigningError>
    sign_digest(std::span<const uint8_t, 32>, const nordiska::SigningContext&, double* call_seconds) override {
        if (call_seconds != nullptr) {
            *call_seconds += 0.0; // stub: zero crypto latency by design
        }
        return kStubHex;
    }

  private:
    static const std::string kStubHex;
};

const std::string StubPdfSigner::kStubHex(8192, '0');

using Clock = std::chrono::steady_clock;

std::size_t get_proc_status_field_bytes(std::string_view field_prefix) {
    std::ifstream status("/proc/self/status");
    std::string line;
    while (std::getline(status, line)) {
        if (line.starts_with(field_prefix)) {
            const auto colon = line.find(':');
            if (colon != std::string::npos) {
                std::string val_str = line.substr(colon + 1);
                // Strip leading whitespace
                const auto first_digit = val_str.find_first_of("0123456789");
                if (first_digit != std::string::npos) {
                    const std::size_t kb = std::stoull(val_str.substr(first_digit));
                    return kb * 1024;
                }
            }
        }
    }
    return 0;
}

std::size_t get_current_rss_bytes() {
    return get_proc_status_field_bytes("VmRSS:");
}

std::size_t get_peak_rss_bytes() {
    return get_proc_status_field_bytes("VmHWM:");
}

struct PayloadInfo {
    std::vector<uint8_t> bytes;
    std::size_t doc_count{0};
    std::size_t tx_count{0};
};

struct Options {
    std::filesystem::path input_path;
    std::string api = "cabi"; // "cabi" or "direct"
    std::string renderer = "haru";
    std::string ingestor = "simdjson";
    bool compression = true;
    bool signing = false;
    std::size_t target_customers = 0; // if > 0, cycles through payloads until target_customers reached
    std::size_t iterations = 1;
    std::size_t warmups = 1;
    std::size_t workers = 1;
    bool instrumented = false;
};

void print_usage(std::string_view prog_name) {
    std::cout
        << "Usage: " << prog_name << " [input-file-or-dir] [options]\n\n"
        << "Options:\n"
        << "  -i, --input <path>          Path to JSON input file or directory (default: auto-detects pool_100)\n"
        << "  --api <cabi|direct>         Execution API (default: cabi)\n"
        << "  --target-customers <N>      Target customer count to process across workers (default: 0 = exact pool "
           "size)\n"
        << "  --workers <N>               Worker threads count (default: 1)\n"
        << "  --renderer <haru|cairo|native> Rendering engine (default: haru)\n"
        << "  --ingestor <simd|nlohmann>  JSON ingestor (default: simdjson)\n"
        << "  --no-compression            Disable Flate stream compression in PDF rendering\n"
        << "  --compression <true|false>  Configure PDF stream compression (default: true)\n"
        << "  --signing                   Use fixed CMS stub and embed into each PDF (default: false)\n"
        << "  --instrumented              Enable phase profiling, including hashing and signer call\n"
        << "  --iterations <N>            Measurement iterations (default: 1)\n"
        << "  --warmups <N>               Warmup iterations (default: 1)\n"
        << "  -h, --help                  Print this help message\n";
}

std::filesystem::path resolve_default_pool_path() {
    const std::vector<std::filesystem::path> candidates = {"tools/synthetic-input-generator/generated/pool_100",
                                                           "../tools/synthetic-input-generator/generated/pool_100",
                                                           "../../tools/synthetic-input-generator/generated/pool_100"};
    for (const auto& p : candidates) {
        if (std::filesystem::exists(p)) {
            return p;
        }
    }
    return candidates[0];
}

Options parse_options(int argc, char* argv[]) {
    Options options;

    for (int index = 1; index < argc; ++index) {
        const std::string_view arg = argv[index];
        auto next_value = [&](std::string_view opt_name) {
            if (++index >= argc) {
                throw std::invalid_argument(std::string(opt_name) + " requires a value");
            }
            return std::string(argv[index]);
        };

        if (arg == "-h" || arg == "--help") {
            print_usage(argv[0]);
            std::exit(EXIT_SUCCESS);
        } else if (arg == "-i" || arg == "--input") {
            options.input_path = next_value(arg);
        } else if (arg == "--api") {
            options.api = next_value("--api");
        } else if (arg == "--target-customers") {
            options.target_customers = std::stoull(next_value("--target-customers"));
        } else if (arg == "--workers") {
            options.workers = std::stoull(next_value("--workers"));
        } else if (arg == "--renderer") {
            options.renderer = next_value("--renderer");
        } else if (arg == "--ingestor") {
            options.ingestor = next_value("--ingestor");
        } else if (arg == "--no-compression") {
            options.compression = false;
        } else if (arg == "--compression") {
            const std::string val = next_value("--compression");
            if (val == "false" || val == "0" || val == "no") {
                options.compression = false;
            } else if (val == "true" || val == "1" || val == "yes") {
                options.compression = true;
            } else {
                throw std::invalid_argument("invalid value for --compression: " + val);
            }
        } else if (arg == "--signing") {
            options.signing = true;
        } else if (arg == "--instrumented") {
            options.instrumented = true;
        } else if (arg == "--iterations") {
            options.iterations = std::stoull(next_value("--iterations"));
        } else if (arg == "--warmups") {
            options.warmups = std::stoull(next_value("--warmups"));
        } else if (!arg.starts_with("-")) {
            if (options.input_path.empty()) {
                options.input_path = arg;
            } else {
                throw std::invalid_argument("unexpected extra argument: " + std::string(arg));
            }
        } else {
            throw std::invalid_argument("unknown option: " + std::string(arg));
        }
    }

    if (options.input_path.empty()) {
        options.input_path = resolve_default_pool_path();
    }

    return options;
}

std::vector<PayloadInfo> load_payloads(const std::filesystem::path& path) {
    std::vector<std::filesystem::path> files;
    if (std::filesystem::is_directory(path)) {
        for (const auto& entry : std::filesystem::directory_iterator(path)) {
            if (entry.is_regular_file() && entry.path().extension() == ".json") {
                // Ignore manifest files
                if (entry.path().filename().string().starts_with("manifest")) {
                    continue;
                }
                files.push_back(entry.path());
            }
        }
    } else if (std::filesystem::is_regular_file(path)) {
        files.push_back(path);
    } else {
        throw std::runtime_error("Path does not exist: " + path.string());
    }

    std::vector<PayloadInfo> payloads;
    payloads.reserve(files.size());
    for (const auto& file_path : files) {
        std::ifstream stream(file_path, std::ios::binary);
        if (!stream) {
            throw std::runtime_error("Failed to open file: " + file_path.string());
        }
        std::vector<uint8_t> bytes((std::istreambuf_iterator<char>(stream)), std::istreambuf_iterator<char>());

        // Inspect metadata once at startup for accurate transaction/doc stats
        std::size_t doc_count = 0;
        std::size_t tx_count = 0;
        try {
            auto parsed = nlohmann::json::parse(bytes.begin(), bytes.end());
            if (parsed.contains("documents") && parsed["documents"].is_array()) {
                doc_count = parsed["documents"].size();
                for (const auto& doc_entry : parsed["documents"]) {
                    if (doc_entry.contains("document") && doc_entry["document"].contains("transactions") &&
                        doc_entry["document"]["transactions"].is_array()) {
                        tx_count += doc_entry["document"]["transactions"].size();
                    }
                }
            }
        } catch (...) {
            // Keep zero counts if parsing fails here; ingestor will catch it during benchmark
        }

        payloads.push_back(PayloadInfo{
            .bytes = std::move(bytes),
            .doc_count = doc_count,
            .tx_count = tx_count,
        });
    }
    return payloads;
}

struct CallbackState {
    std::size_t docs_delivered{0};
    std::size_t bytes_delivered{0};
    std::size_t max_doc_bytes{0};
};

int cabi_delivery_callback(const struct nordiska_pdf_batch_view* batch, void* user_data) {
    if (batch == nullptr) {
        return -1;
    }
    if (user_data != nullptr) {
        auto* state = static_cast<CallbackState*>(user_data);
        state->docs_delivered += batch->document_count;
        for (std::size_t i = 0; i < batch->document_count; ++i) {
            const auto len = batch->documents[i].length;
            state->bytes_delivered += len;
            if (len > state->max_doc_bytes) {
                state->max_doc_bytes = len;
            }
        }
    }
    return 0; // Return 0: accept and deliver
}

struct WorkerStats {
    std::size_t customers{0};
    std::size_t docs{0};
    std::size_t txs{0};
    std::size_t bytes{0};
    std::size_t max_batch_bytes{0};
    std::size_t max_doc_bytes{0};
    nordiska::PipelineTiming timing{};
};

} // namespace

int main(int argc, char* argv[]) {
    try {
        const Options options = parse_options(argc, argv);

        const std::size_t initial_rss = get_current_rss_bytes();
        const auto payloads = load_payloads(options.input_path);
        const std::size_t input_loaded_rss = get_current_rss_bytes();

        if (payloads.empty()) {
            std::cerr << "No valid JSON customer payload files found at " << options.input_path << "\n";
            return EXIT_FAILURE;
        }

        std::size_t total_input_payload_bytes = 0;
        std::size_t min_input_payload_bytes = payloads[0].bytes.size();
        std::size_t max_input_payload_bytes = 0;
        for (const auto& p : payloads) {
            total_input_payload_bytes += p.bytes.size();
            if (p.bytes.size() < min_input_payload_bytes) {
                min_input_payload_bytes = p.bytes.size();
            }
            if (p.bytes.size() > max_input_payload_bytes) {
                max_input_payload_bytes = p.bytes.size();
            }
        }

        const std::size_t customers_to_run =
            (options.target_customers > 0) ? options.target_customers : payloads.size();

        const bool use_direct = (options.api != "cabi") || options.signing || options.ingestor != "simdjson" ||
                                options.instrumented || !options.compression || (options.renderer != "haru");
        std::string api_mode_str = use_direct ? "direct C++" : "cabi";
        if (!options.compression && options.api == "cabi" && !options.instrumented) {
            api_mode_str = "direct C++ (uncompressed override)";
        } else if (options.renderer != "haru" && options.api == "cabi" && !options.instrumented) {
            api_mode_str = "direct C++ (renderer override)";
        }

        std::cout << "Loaded " << payloads.size() << " customer payload(s) (" << std::fixed << std::setprecision(2)
                  << (static_cast<double>(total_input_payload_bytes) / (1024.0 * 1024.0)) << " MB) into RAM.\n"
                  << "Benchmark mode: API=" << api_mode_str << ", workers=" << options.workers
                  << ", target_customers=" << customers_to_run << ", renderer=" << options.renderer
                  << ", ingestor=" << options.ingestor << ", compression=" << (options.compression ? "true" : "false")
                  << ", signing=" << (options.signing ? "true" : "false")
                  << (options.instrumented ? ", instrumented=true" : "") << "\n\n";

        nordiska::GeneratorConfig direct_config;
        direct_config.compression = options.compression;
        direct_config.enable_signing = options.signing;
        if (options.signing) {
            direct_config.custom_signer = std::make_shared<StubPdfSigner>();
        }
        if (options.renderer == "haru") {
            direct_config.engine = nordiska::PdfEngineKind::Libharu;
        } else if (options.renderer == "cairo") {
            direct_config.engine = nordiska::PdfEngineKind::Cairo;
        } else if (options.renderer == "native" || options.renderer == "fast") {
            direct_config.engine = nordiska::PdfEngineKind::Native;
        } else {
            std::cerr << "Unsupported renderer: " << options.renderer << "\n";
            return EXIT_FAILURE;
        }

        if (options.ingestor == "nlohmann") {
            direct_config.ingestor = nordiska::JsonIngestorKind::Nlohmann;
        } else if (options.ingestor == "simdjson" || options.ingestor == "simd") {
            direct_config.ingestor = nordiska::JsonIngestorKind::Simdjson;
        } else {
            std::cerr << "Unsupported ingestor: " << options.ingestor << "\n";
            return EXIT_FAILURE;
        }

        // Warmup
        for (std::size_t warmup = 0; warmup < options.warmups; ++warmup) {
            const auto& sample = payloads[0];
            if (!use_direct) {
                CallbackState cb_state;
                int status = nordiska_pdf_v1_generate_customer_batch(sample.bytes.data(), sample.bytes.size(),
                                                                     cabi_delivery_callback, &cb_state);
                if (status != 0) {
                    std::cerr << "Warmup error via C ABI: " << nordiska_pdf_v1_get_last_error() << "\n";
                    return EXIT_FAILURE;
                }
            } else {
                const nordiska::PdfGenerator generator(direct_config);
                auto res = generator.generate(sample.bytes);
                if (!res) {
                    std::cerr << "Warmup error: " << res.error().message << "\n";
                    return EXIT_FAILURE;
                }
            }
        }

        for (std::size_t iter = 0; iter < options.iterations; ++iter) {
            std::atomic<std::size_t> next_customer_idx{0};
            std::atomic<bool> abort_requested{false};
            std::string first_error_msg;
            std::mutex error_mutex;

            const std::size_t num_workers = std::max<std::size_t>(1, options.workers);
            std::vector<WorkerStats> worker_stats(num_workers);

            const auto start_time = Clock::now();

            auto worker_lambda = [&](std::size_t worker_id) {
                // For direct C++ API or instrumented profiling, instantiate a per-thread generator instance
                std::unique_ptr<nordiska::PdfGenerator> direct_generator;
                if (use_direct) {
                    direct_generator = std::make_unique<nordiska::PdfGenerator>(direct_config);
                }

                auto& stats = worker_stats[worker_id];
                nordiska::PipelineTiming* p_timing = options.instrumented ? &stats.timing : nullptr;

                while (true) {
                    if (abort_requested.load(std::memory_order_relaxed)) {
                        break;
                    }

                    const std::size_t current_idx = next_customer_idx.fetch_add(1, std::memory_order_relaxed);
                    if (current_idx >= customers_to_run) {
                        break;
                    }

                    const auto& payload = payloads[current_idx % payloads.size()];

                    if (!use_direct) {
                        CallbackState cb_state;
                        const int status = nordiska_pdf_v1_generate_customer_batch(
                            payload.bytes.data(), payload.bytes.size(), cabi_delivery_callback, &cb_state);

                        if (status != 0) {
                            std::lock_guard<std::mutex> lock(error_mutex);
                            if (!abort_requested.load(std::memory_order_relaxed)) {
                                first_error_msg = std::string("C ABI failure (status ") +
                                                  nordiska_pdf_v1_status_name(status) +
                                                  "): " + nordiska_pdf_v1_get_last_error();
                                abort_requested.store(true, std::memory_order_relaxed);
                            }
                            break;
                        }

                        stats.customers += 1;
                        stats.docs += cb_state.docs_delivered;
                        stats.bytes += cb_state.bytes_delivered;
                        stats.txs += payload.tx_count;
                        if (cb_state.bytes_delivered > stats.max_batch_bytes) {
                            stats.max_batch_bytes = cb_state.bytes_delivered;
                        }
                        if (cb_state.max_doc_bytes > stats.max_doc_bytes) {
                            stats.max_doc_bytes = cb_state.max_doc_bytes;
                        }
                    } else {
                        auto result = direct_generator->generate(payload.bytes, p_timing);
                        if (!result) {
                            std::lock_guard<std::mutex> lock(error_mutex);
                            if (!abort_requested.load(std::memory_order_relaxed)) {
                                first_error_msg = result.error().message;
                                abort_requested.store(true, std::memory_order_relaxed);
                            }
                            break;
                        }

                        stats.customers += 1;
                        stats.docs += result->documents.size();
                        stats.txs += payload.tx_count;
                        std::size_t batch_bytes = 0;
                        for (const auto& doc : result->documents) {
                            const auto doc_len = doc.pdf_bytes.size();
                            batch_bytes += doc_len;
                            stats.bytes += doc_len;
                            if (doc_len > stats.max_doc_bytes) {
                                stats.max_doc_bytes = doc_len;
                            }
                        }
                        if (batch_bytes > stats.max_batch_bytes) {
                            stats.max_batch_bytes = batch_bytes;
                        }
                    }
                }
            };

            std::vector<std::thread> threads;
            threads.reserve(num_workers);
            for (std::size_t w = 0; w < num_workers; ++w) {
                threads.emplace_back(worker_lambda, w);
            }
            for (auto& t : threads) {
                t.join();
            }

            const auto end_time = Clock::now();
            const double elapsed_seconds = std::chrono::duration<double>(end_time - start_time).count();

            if (abort_requested.load()) {
                std::cerr << "Benchmark aborted on error: " << first_error_msg << "\n";
                return EXIT_FAILURE;
            }

            // Aggregate stats across workers
            std::size_t total_customers = 0;
            std::size_t total_docs = 0;
            std::size_t total_txs = 0;
            std::size_t total_bytes = 0;
            std::size_t max_batch_bytes = 0;
            std::size_t max_doc_bytes = 0;
            double total_cpu_ingest = 0.0;
            double total_cpu_layout = 0.0;
            double total_cpu_render = 0.0;
            double total_sign = 0.0;
            double total_signer_call = 0.0;

            for (const auto& ws : worker_stats) {
                total_customers += ws.customers;
                total_docs += ws.docs;
                total_txs += ws.txs;
                total_bytes += ws.bytes;
                if (ws.max_batch_bytes > max_batch_bytes) {
                    max_batch_bytes = ws.max_batch_bytes;
                }
                if (ws.max_doc_bytes > max_doc_bytes) {
                    max_doc_bytes = ws.max_doc_bytes;
                }
                if (options.instrumented) {
                    total_cpu_ingest += ws.timing.ingest_seconds;
                    total_cpu_layout += ws.timing.layout_seconds;
                    total_cpu_render += ws.timing.render_seconds;
                    total_sign += ws.timing.sign_seconds;
                    total_signer_call += ws.timing.signer_call_seconds;
                }
            }

            const double total_ms = elapsed_seconds * 1000.0;
            const double ms_per_customer = (total_customers > 0) ? (total_ms / total_customers) : 0.0;
            const double customers_per_sec = (elapsed_seconds > 0.0) ? (total_customers / elapsed_seconds) : 0.0;

            const double ms_per_doc = (total_docs > 0) ? (total_ms / total_docs) : 0.0;
            const double docs_per_sec = (elapsed_seconds > 0.0) ? (total_docs / elapsed_seconds) : 0.0;

            const double txs_per_sec = (elapsed_seconds > 0.0) ? (total_txs / elapsed_seconds) : 0.0;
            const double mb_generated = static_cast<double>(total_bytes) / (1024.0 * 1024.0);
            const double mb_per_sec = (elapsed_seconds > 0.0) ? (mb_generated / elapsed_seconds) : 0.0;

            // Memory measurements
            const std::size_t peak_rss_bytes = get_peak_rss_bytes();
            const std::size_t worker_heap_delta_bytes =
                (peak_rss_bytes > input_loaded_rss) ? (peak_rss_bytes - input_loaded_rss) : 0;
            const double per_worker_delta_mb =
                (num_workers > 0) ? ((static_cast<double>(worker_heap_delta_bytes) / (1024.0 * 1024.0)) / num_workers)
                                  : 0.0;

            std::cout << "--- Benchmark Results (Iteration " << (iter + 1) << ") ---\n"
                      << "  Total wall time:       " << std::fixed << std::setprecision(2) << total_ms << " ms ("
                      << elapsed_seconds << " s)\n"
                      << "  Customers processed:   " << total_customers << "\n"
                      << "  Throughput (customers):" << std::setprecision(1) << customers_per_sec << " cust/sec ("
                      << std::setprecision(2) << ms_per_customer << " ms/customer)\n"
                      << "  Documents generated:   " << total_docs << "\n"
                      << "  Throughput (documents):" << std::setprecision(1) << docs_per_sec << " docs/sec ("
                      << std::setprecision(2) << ms_per_doc << " ms/doc)\n"
                      << "  Transactions simulated:" << total_txs << " (" << std::setprecision(1) << txs_per_sec
                      << " tx/sec)\n\n";

            if (options.instrumented) {
                const auto report = [&](const char* label, double seconds) {
                    std::cout << "  " << std::left << std::setw(30) << label << std::right << std::fixed
                              << std::setprecision(3) << seconds * 1000.0 << " ms total; "
                              << (total_docs ? seconds * 1e6 / total_docs : 0.0) << " us/doc\n";
                };
                const auto sum = [&](double nordiska::PipelineTiming::*field) {
                    double value = 0;
                    for (const auto& ws : worker_stats) {
                        value += ws.timing.*field;
                    }
                    return value;
                };
                const auto prep_sum = [&](double nordiska::SignaturePreparationTiming::*field) {
                    double value = 0;
                    for (const auto& ws : worker_stats) {
                        value += ws.timing.preparation.*field;
                    }
                    return value;
                };
                std::cout << "--- Summed worker elapsed times (not CPU time) ---\n"
                          << "  Nested rows are included in their parent; us/doc is worker time, not latency.\n";
                report("Ingestion", total_cpu_ingest);
                report("Layout", total_cpu_layout);
                report("Render", total_cpu_render);
                report("Signing pipeline (inclusive)", total_sign);
                report("  Preparation (inclusive)", sum(&nordiska::PipelineTiming::prepare_seconds));
                report("    Locate xref", prep_sum(&nordiska::SignaturePreparationTiming::locate_xref_seconds));
                report("    Trailer parse", prep_sum(&nordiska::SignaturePreparationTiming::trailer_seconds));
                report("    Xref entries", prep_sum(&nordiska::SignaturePreparationTiming::xref_entries_seconds));
                report("    Catalog parse", prep_sum(&nordiska::SignaturePreparationTiming::catalog_seconds));
                report("    Metadata copy", prep_sum(&nordiska::SignaturePreparationTiming::metadata_copy_seconds));
                report("    Format update / ByteRange",
                       prep_sum(&nordiska::SignaturePreparationTiming::format_seconds));
                report("    Reserve / write buffer",
                       prep_sum(&nordiska::SignaturePreparationTiming::buffer_write_seconds));
                report("  Signing digest", sum(&nordiska::PipelineTiming::digest_seconds));
                report("  Signer wrapper (inclusive)", sum(&nordiska::PipelineTiming::signer_wrapper_seconds));
                report("    External call (reported)", total_signer_call);
                report("  CMS insertion", sum(&nordiska::PipelineTiming::insert_seconds));
                report("  Final artifact checksum", sum(&nordiska::PipelineTiming::checksum_seconds));
                const double accounted =
                    sum(&nordiska::PipelineTiming::prepare_seconds) + sum(&nordiska::PipelineTiming::digest_seconds) +
                    sum(&nordiska::PipelineTiming::signer_wrapper_seconds) +
                    sum(&nordiska::PipelineTiming::insert_seconds) + sum(&nordiska::PipelineTiming::checksum_seconds);
                report("  Signing unassigned", total_sign - accounted);
                report("Pipeline measured total", total_cpu_ingest + total_cpu_layout + total_cpu_render + total_sign);
                std::cout << "  Wall time also includes thread/generator lifecycle, result disposal, bookkeeping,\n"
                          << "  unmeasured glue, timer overhead and scheduling. No phase/wall equivalence assumed.\n\n";
            }

            std::cout << "--- Memory Utilization & High Watermark ---\n"
                      << "  Input RAM footprint:   " << std::setprecision(2)
                      << (static_cast<double>(total_input_payload_bytes) / (1024.0 * 1024.0)) << " MB ("
                      << payloads.size() << " payloads, avg "
                      << (static_cast<double>(total_input_payload_bytes) / payloads.size() / 1024.0)
                      << " KB/batch, max " << (static_cast<double>(max_input_payload_bytes) / 1024.0) << " KB)\n"
                      << "  Baseline Process RSS:  " << std::setprecision(2)
                      << (static_cast<double>(input_loaded_rss) / (1024.0 * 1024.0))
                      << " MB (after loading input payloads)\n"
                      << "  Peak RSS (high-water): " << std::setprecision(2)
                      << (static_cast<double>(peak_rss_bytes) / (1024.0 * 1024.0))
                      << " MB (maximum physical RAM mapped)\n"
                      << "  Worker RAM overhead:   " << std::setprecision(2)
                      << (static_cast<double>(worker_heap_delta_bytes) / (1024.0 * 1024.0)) << " MB (~"
                      << per_worker_delta_mb << " MB / worker across " << num_workers << " thread(s))\n\n"
                      << "--- Output Data Footprint ---\n"
                      << "  Total PDF generated:   " << std::setprecision(2) << mb_generated << " MB ("
                      << std::setprecision(2) << mb_per_sec << " MB/sec)\n"
                      << "  Average customer batch:" << std::setprecision(2)
                      << (total_customers > 0 ? (static_cast<double>(total_bytes) / total_customers / 1024.0) : 0.0)
                      << " KB / customer\n"
                      << "  Peak customer batch:   " << std::setprecision(2)
                      << (static_cast<double>(max_batch_bytes) / 1024.0) << " KB\n"
                      << "  Average document size: " << std::setprecision(2)
                      << (total_docs > 0 ? (static_cast<double>(total_bytes) / total_docs / 1024.0) : 0.0)
                      << " KB / doc\n"
                      << "  Peak document size:    " << std::setprecision(2)
                      << (static_cast<double>(max_doc_bytes) / 1024.0) << " KB\n\n";
        }

        return EXIT_SUCCESS;
    } catch (const std::exception& error) {
        std::cerr << "Fatal error: " << error.what() << "\n";
        return EXIT_FAILURE;
    }
}
