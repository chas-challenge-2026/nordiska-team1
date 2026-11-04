#include "nordiska/application/pdf_generator.hpp"
#include "nordiska/ingestion/json_ingestor.hpp"
#include "nordiska/rendering/pdf_engine.hpp"
#include "nordiska/signing/pdf_signer.hpp"

#include <algorithm>
#include <chrono>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <memory>
#include <random>
#include <span>
#include <string>
#include <string_view>
#include <vector>

namespace {

void require(bool condition, const char* message) {
    if (!condition) {
        std::fprintf(stderr, "Contract failure in fuzz_combinatorial_pipeline: %s\n", message);
        std::abort();
    }
}

// Custom Mock Signer for testing signed configurations
class FuzzMockSigner final : public nordiska::PdfSigner {
  public:
    enum class Mode { Valid, OddLength, NonHex, Empty, Oversized, Error };

    explicit FuzzMockSigner(Mode mode = Mode::Valid, size_t valid_hex_len = 8192)
        : mode_(mode), valid_hex_len_(valid_hex_len) {}

    std::expected<std::string, nordiska::SigningError> sign_digest(std::span<const uint8_t, 32> digest,
                                                                   double* call_seconds) override {
        if (call_seconds) {
            *call_seconds += 0.0001;
        }

        switch (mode_) {
        case Mode::Valid: {
            std::string hex(valid_hex_len_, 'a');
            // Embed first few bytes of digest in hex to make it dynamic
            constexpr char hex_chars[] = "0123456789abcdef";
            for (size_t i = 0; i < std::min<size_t>(digest.size(), valid_hex_len_ / 2); ++i) {
                hex[i * 2] = hex_chars[digest[i] >> 4];
                hex[i * 2 + 1] = hex_chars[digest[i] & 0x0F];
            }
            return hex;
        }
        case Mode::OddLength:
            return std::string("abcde");
        case Mode::NonHex:
            return std::string("zzzz");
        case Mode::Empty:
            return std::string("");
        case Mode::Oversized:
            return std::string(valid_hex_len_ + 100, 'f');
        case Mode::Error:
            return std::unexpected(nordiska::SigningError{
                .kind = nordiska::SigningErrorKind::SignatureGenerationFailed,
                .code = nordiska::DiagnosticCode::UnexpectedFailure,
            });
        }
        return std::string(valid_hex_len_, '0');
    }

  private:
    Mode mode_;
    size_t valid_hex_len_;
};

// Generates structured valid JSON with controllable parameters
std::string generate_structured_batch(uint64_t customer_id, size_t doc_count, size_t tx_count,
                                      const std::string& custom_text = "Test") {
    std::string json = "{\n";
    json += "  \"customer_id\": " + std::to_string(customer_id) + ",\n";
    json += "  \"customer_name\": \"" + custom_text + "\",\n";
    json += "  \"schema_version\": \"1.0\",\n";
    json += "  \"created_at\": \"2026-10-19T12:00:00Z\",\n";
    json += "  \"documents\": [\n";

    for (size_t d = 0; d < doc_count; ++d) {
        if (d > 0) {
            json += ",\n";
        }
        const std::string doc_id = "doc_" + std::to_string(d);
        if (d % 2 == 0) {
            // Account Statement
            json += "    {\n";
            json += "      \"document_id\": \"" + doc_id + "\",\n";
            json += "      \"kind\": \"account_statement\",\n";
            json += "      \"document\": {\n";
            json += "        \"title\": \"Kontoutdrag " + custom_text + "\",\n";
            json += "        \"account_number\": \"NKM-" + std::to_string(1000 + d) + "\",\n";
            json += "        \"account_name\": \"Konto " + custom_text + "\",\n";
            json += "        \"period\": \"2026-01-01 - 2026-01-31\",\n";
            json += "        \"currency\": \"SEK\",\n";
            json += "        \"opening_balance\": \"10 000,00 kr\",\n";
            json += "        \"closing_balance\": \"15 000,00 kr\",\n";
            json += "        \"transactions\": [\n";

            for (size_t t = 0; t < tx_count; ++t) {
                if (t > 0) {
                    json += ",\n";
                }
                json += "          {\n";
                json += "            \"date\": \"2026-01-15\",\n";
                json += "            \"type\": \"Kortköp\",\n";
                json += "            \"description\": \"Tx " + std::to_string(t) + " " + custom_text + "\",\n";
                json += "            \"amount_minor\": " + std::to_string(t % 2 == 0 ? 50000 : -25000) + ",\n";
                json += std::string("            \"amount_display\": \"") + (t % 2 == 0 ? "500,00 kr" : "-250,00 kr") +
                        "\",\n";
                json += "            \"balance_after_display\": \"10 500,00 kr\"\n";
                json += "          }";
            }
            json += "\n        ]\n";
            json += "      }\n";
            json += "    }";
        } else {
            // Annual Tax Report
            json += "    {\n";
            json += "      \"document_id\": \"" + doc_id + "\",\n";
            json += "      \"kind\": \"annual_tax_report\",\n";
            json += "      \"document\": {\n";
            json += "        \"title\": \"Kontrolluppgift " + custom_text + "\",\n";
            json += "        \"tax_year\": \"2025\",\n";
            json += "        \"account_number\": \"NKM-" + std::to_string(1000 + d) + "\",\n";
            json += "        \"account_name\": \"Räntekonto " + custom_text + "\",\n";
            json += "        \"total_interest_earned\": \"1 250,50 kr\",\n";
            json += "        \"preliminary_tax_deducted\": \"375,00 kr\",\n";
            json += "        \"reported_to_authority\": \"Skatteverket\"\n";
            json += "      }\n";
            json += "    }";
        }
    }
    json += "\n  ]\n}";
    return json;
}

// Loads golden samples and seeds into corpus
std::vector<std::string> load_seed_corpus() {
    std::vector<std::string> corpus;
    const std::vector<std::string> paths = {
        "tests/fuzz/seeds/account-statement.json",     "tests/fuzz/seeds/annual-tax-report.json",
        "tests/fuzz/seeds/mixed-batch.json",           "tests/fuzz/seeds/pagination-unicode.json",
        "docs/golden_customer_batch_sample.json",      "../docs/golden_customer_batch_sample.json",
        "../../docs/golden_customer_batch_sample.json"};

    for (const auto& path : paths) {
        if (std::filesystem::exists(path)) {
            std::ifstream f(path);
            if (f.is_open()) {
                std::string content((std::istreambuf_iterator<char>(f)), std::istreambuf_iterator<char>());
                if (!content.empty()) {
                    corpus.push_back(std::move(content));
                }
            }
        }
    }
    return corpus;
}

void verify_generated_batch(const nordiska::GeneratedPdfs& batch, const nordiska::GeneratorConfig& config,
                            size_t expected_docs) {
    require(batch.customer_id > 0, "customer_id must be > 0");
    require(batch.documents.size() == expected_docs, "document count mismatch");

    for (const auto& doc : batch.documents) {
        require(!doc.document_id.empty(), "document_id must not be empty");
        require(!doc.pdf_bytes.empty(), "pdf_bytes must not be empty");

        std::string_view pdf(reinterpret_cast<const char*>(doc.pdf_bytes.data()), doc.pdf_bytes.size());
        require(pdf.starts_with("%PDF-"), "must start with %PDF- header");

        const auto end = pdf.find_last_not_of("\r\n\t ");
        require(end != std::string_view::npos && pdf.substr(0, end + 1).ends_with("%%EOF"), "must end with %%EOF");

        // Artifact SHA-256 must be computed
        bool hash_nonzero = std::any_of(doc.sha256_hash.begin(), doc.sha256_hash.end(), [](auto b) { return b != 0; });
        require(hash_nonzero, "artifact sha256_hash must be non-zero");

        if (config.enable_signing) {
            require(doc.signature_slot.is_signed, "signed configuration must mark slot signed");
            bool sig_nonzero =
                std::any_of(doc.signing_digest.begin(), doc.signing_digest.end(), [](auto b) { return b != 0; });
            require(sig_nonzero, "signed configuration must compute signing_digest");
        } else {
            require(!doc.signature_slot.is_signed, "unsigned configuration must not mark slot signed");
            bool sig_allzero =
                std::all_of(doc.signing_digest.begin(), doc.signing_digest.end(), [](auto b) { return b == 0; });
            require(sig_allzero, "unsigned configuration must leave signing_digest zero");
        }
    }
}

// Fuzzes all combinations of settings with structured inputs
void test_pipeline_matrix_structured() {
    const std::vector<nordiska::JsonIngestorKind> ingestors = {
        nordiska::JsonIngestorKind::Simdjson,
        nordiska::JsonIngestorKind::Nlohmann,
    };
    const std::vector<nordiska::PdfEngineKind> engines = {
        nordiska::PdfEngineKind::Libharu,
        nordiska::PdfEngineKind::Cairo,
        nordiska::PdfEngineKind::Native,
    };
    const std::vector<bool> compressions = {true, false};
    const std::vector<bool> signings = {false, true};

    // Stress transaction counts: 0, 1, 10, 80 (multi-page), 300 (stress bump arena and page limits)
    const std::vector<size_t> tx_counts = {0, 1, 10, 80, 300};

    for (auto ingestor : ingestors) {
        for (auto engine : engines) {
            for (bool compression : compressions) {
                for (bool signing : signings) {
                    nordiska::GeneratorConfig config{
                        .ingestor = ingestor,
                        .engine = engine,
                        .enable_signing = signing,
                        .compression = compression,
                        .signature_contents_capacity = 8192,
                    };
                    if (signing) {
                        config.custom_signer = std::make_shared<FuzzMockSigner>(FuzzMockSigner::Mode::Valid, 8192);
                    }

                    nordiska::PdfGenerator generator(config);

                    for (size_t tx_count : tx_counts) {
                        // Swedish + CP1252 + Unmapped Unicode
                        const std::string text = "Nordiska Sparbanken — åäö ÅÄÖ € • 100% (Test)";
                        std::string json = generate_structured_batch(12345, 2, tx_count, text);
                        std::span<const uint8_t> span(reinterpret_cast<const uint8_t*>(json.data()), json.size());

                        nordiska::PipelineTiming timing{};
                        auto res = generator.generate(span, &timing);
                        require(res.has_value(), "generator.generate must succeed on valid structured batch");
                        verify_generated_batch(*res, config, 2);
                        require(timing.total_seconds() >= 0.0, "timing must be valid");
                    }
                }
            }
        }
    }
}

// Test differential consistency between Simdjson and Nlohmann on identical payloads
void test_differential_ingestion() {
    nordiska::JsonIngestor simd_ingestor(nordiska::JsonIngestorKind::Simdjson);
    nordiska::JsonIngestor nlohmann_ingestor(nordiska::JsonIngestorKind::Nlohmann);

    // Test large unsigned customer_id (boundary of uint64_t)
    const std::vector<uint64_t> customer_ids = {
        1, 42, 1000000, 2147483647ULL, 4294967295ULL, 9223372036854775807ULL, 18446744073709551615ULL};

    for (uint64_t cid : customer_ids) {
        std::string json = generate_structured_batch(cid, 2, 5);
        std::span<const uint8_t> span(reinterpret_cast<const uint8_t*>(json.data()), json.size());

        auto res_simd = simd_ingestor.ingest(span);
        auto res_nlohmann = nlohmann_ingestor.ingest(span);

        require(res_simd.has_value() == res_nlohmann.has_value(),
                "Simdjson and Nlohmann must agree on valid/invalid outcome");
        if (res_simd.has_value()) {
            require(res_simd->customer_id == res_nlohmann->customer_id,
                    "Simdjson and Nlohmann must produce identical customer_id");
            require(res_simd->documents.size() == res_nlohmann->documents.size(),
                    "Simdjson and Nlohmann must produce identical document count");
        }
    }
}

// Test signer failure modes in signed configuration
void test_signing_failure_modes() {
    const std::vector<FuzzMockSigner::Mode> bad_modes = {
        FuzzMockSigner::Mode::OddLength, FuzzMockSigner::Mode::NonHex, FuzzMockSigner::Mode::Empty,
        FuzzMockSigner::Mode::Oversized, FuzzMockSigner::Mode::Error,
    };

    for (auto mode : bad_modes) {
        nordiska::GeneratorConfig config{
            .ingestor = nordiska::JsonIngestorKind::Simdjson,
            .engine = nordiska::PdfEngineKind::Native,
            .enable_signing = true,
            .compression = true,
            .custom_signer = std::make_shared<FuzzMockSigner>(mode, 8192),
            .signature_contents_capacity = 8192,
        };
        nordiska::PdfGenerator generator(config);
        std::string json = generate_structured_batch(100, 1, 2);
        std::span<const uint8_t> span(reinterpret_cast<const uint8_t*>(json.data()), json.size());

        auto res = generator.generate(span);
        require(!res.has_value(), "Generator must fail when signer returns invalid hex or error");
    }

    // Invalid capacity (odd, 0, or too large)
    {
        nordiska::GeneratorConfig config_odd{
            .enable_signing = true,
            .signature_contents_capacity = 8191, // odd
        };
        nordiska::PdfGenerator gen_odd(config_odd);
        std::string json = generate_structured_batch(100, 1, 1);
        std::span<const uint8_t> span(reinterpret_cast<const uint8_t*>(json.data()), json.size());
        auto res = gen_odd.generate(span);
        require(!res.has_value(), "Generator must reject odd signature capacity");
    }
    {
        nordiska::GeneratorConfig config_zero{
            .enable_signing = true,
            .signature_contents_capacity = 0,
        };
        nordiska::PdfGenerator gen_zero(config_zero);
        std::string json = generate_structured_batch(100, 1, 1);
        std::span<const uint8_t> span(reinterpret_cast<const uint8_t*>(json.data()), json.size());
        auto res = gen_zero.generate(span);
        require(!res.has_value(), "Generator must reject zero signature capacity");
    }
}

// Random byte mutation fuzzing across golden seed corpus
void test_raw_mutation_fuzzing(const std::vector<std::string>& corpus, size_t iterations) {
    std::mt19937 rng(1337);
    std::uniform_int_distribution<size_t> op_dist(0, 3);

    const std::vector<nordiska::PdfEngineKind> engines = {
        nordiska::PdfEngineKind::Libharu,
        nordiska::PdfEngineKind::Cairo,
        nordiska::PdfEngineKind::Native,
    };

    for (size_t i = 0; i < iterations; ++i) {
        if (corpus.empty()) {
            break;
        }
        const std::string& base = corpus[i % corpus.size()];
        std::vector<uint8_t> mutated(base.begin(), base.end());

        // Perform 1 to 4 random mutations
        const size_t num_mutations = (rng() % 4) + 1;
        for (size_t m = 0; m < num_mutations && !mutated.empty(); ++m) {
            const size_t pos = rng() % mutated.size();
            switch (op_dist(rng)) {
            case 0: // bit flip
                mutated[pos] ^= static_cast<uint8_t>(1 << (rng() % 8));
                break;
            case 1: // random byte
                mutated[pos] = static_cast<uint8_t>(rng() & 0xFF);
                break;
            case 2: // delete
                mutated.erase(mutated.begin() + pos);
                break;
            case 3: // insert
                mutated.insert(mutated.begin() + pos, static_cast<uint8_t>(rng() & 0xFF));
                break;
            }
        }

        const auto engine = engines[i % engines.size()];
        const bool signing = (i % 2 == 0);
        nordiska::GeneratorConfig config{
            .ingestor = (i % 3 == 0) ? nordiska::JsonIngestorKind::Nlohmann : nordiska::JsonIngestorKind::Simdjson,
            .engine = engine,
            .enable_signing = signing,
            .compression = (i % 5 != 0),
            .signature_contents_capacity = 8192,
        };
        if (signing) {
            config.custom_signer = std::make_shared<FuzzMockSigner>(FuzzMockSigner::Mode::Valid, 8192);
        }

        nordiska::PdfGenerator generator(config);
        auto res = generator.generate(mutated);
        // Mutated input must either be cleanly rejected or cleanly generate valid PDF without crash
        if (res.has_value()) {
            verify_generated_batch(*res, config, res->documents.size());
        }
    }
}

} // namespace

int main() {
    std::printf("[*] Starting fuzz_combinatorial_pipeline...\n");

    std::printf("  -> Testing combinatorial pipeline matrix with structured layouts...\n");
    test_pipeline_matrix_structured();

    std::printf("  -> Testing differential ingestion consistency between Simdjson & Nlohmann...\n");
    test_differential_ingestion();

    std::printf("  -> Testing signing failure modes & capacity validation...\n");
    test_signing_failure_modes();

    std::printf("  -> Loading seed corpus...\n");
    auto corpus = load_seed_corpus();
    std::printf("     Loaded %zu seed documents\n", corpus.size());

    std::printf("  -> Running 4,000 raw mutation iterations across all engines...\n");
    test_raw_mutation_fuzzing(corpus, 4000);

    std::printf("[+] fuzz_combinatorial_pipeline completed successfully with 0 crashes!\n");
    return 0;
}
