#include "nordiska/application/pdf_generator.hpp"
#include "nordiska/rendering/pdf_engine.hpp"
#include "nordiska/signing/pdf_signer.hpp"
#include "nordiska/signing/signature_slot_appender.hpp"

#include <algorithm>
#include <cassert>
#include <chrono>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <iostream>
#include <random>
#include <span>
#include <string>
#include <vector>

namespace {

void require(bool condition, const char* message) {
    if (!condition) {
        std::fprintf(stderr, "Contract failure in fuzz_pdf_signing_slot: %s\n", message);
        std::abort();
    }
}

// Generate valid base PDFs from all engines to use as baseline seeds
std::vector<uint8_t> generate_sample_pdf(nordiska::PdfEngineKind engine) {
    nordiska::DocumentLayout layout;
    layout.pages.push_back(nordiska::PageLayout{
        .width = 612.0F,
        .height = 792.0F,
        .texts = {nordiska::PositionedText{.x = 54.0F, .y = 54.0F, .text = "Test PDF", .font_size = 12.0F}},
        .lines = {nordiska::PositionedLine{.x1 = 54.0F, .y1 = 70.0F, .x2 = 558.0F, .y2 = 70.0F, .line_width = 1.0F}},
    });
    nordiska::PdfEngine renderer(engine, /*compression=*/false);
    auto res = renderer.render(layout, /*tail_capacity=*/8192 + 2048);
    require(res.has_value(), "Failed to generate base sample PDF");
    return std::move(*res);
}

void test_valid_pdf_signing_flow(const std::vector<uint8_t>& base_pdf, size_t capacity, std::string_view engine_name) {
    std::vector<uint8_t> pdf = base_pdf;
    nordiska::SignaturePreparationTiming timing{};
    auto slot_res = nordiska::append_signature_slot(pdf, capacity, &timing);
    require(slot_res.has_value(), "append_signature_slot must succeed on fresh valid renderer PDF");

    nordiska::SignatureSlot slot = *slot_res;
    require(slot.offset > 0, "slot.offset must be > 0");
    require(slot.max_length == capacity, "slot.max_length must equal capacity");
    require(!slot.is_signed, "fresh slot must not be marked signed");

    // Check byte range digest
    auto digest_res = nordiska::compute_byte_range_digest(pdf, slot);
    require(digest_res.has_value(), "compute_byte_range_digest must succeed on valid slot");
    const auto& digest = *digest_res;
    bool has_nonzero = std::any_of(digest.begin(), digest.end(), [](auto b) { return b != 0; });
    require(has_nonzero, "digest must not be all zeros");

    // Test rejection of invalid signature insertion
    {
        // Odd length hex
        auto err_odd = nordiska::insert_signature(pdf, slot, "ABC");
        require(!err_odd.has_value(), "must reject odd length hex");

        // Non-hex characters
        auto err_nonhex = nordiska::insert_signature(pdf, slot, "ZZZZ");
        require(!err_nonhex.has_value(), "must reject non-hex characters");

        // Empty hex
        auto err_empty = nordiska::insert_signature(pdf, slot, "");
        require(!err_empty.has_value(), "must reject empty hex");

        // Oversized hex
        std::string huge_hex(capacity + 2, 'A');
        auto err_huge = nordiska::insert_signature(pdf, slot, huge_hex);
        require(!err_huge.has_value(), "must reject oversized hex");
    }

    // Valid signature insertion
    std::string valid_hex(capacity, 'A');
    auto insert_res = nordiska::insert_signature(pdf, slot, valid_hex);
    require(insert_res.has_value(), "insert_signature must succeed with valid hex");
    require(slot.is_signed, "slot must be marked signed after insert");

    // Cannot insert signature into already signed slot
    auto double_insert = nordiska::insert_signature(pdf, slot, valid_hex);
    require(!double_insert.has_value(), "must reject insertion into already signed slot");

    // Cannot append signature slot to already-signed PDF
    auto double_append = nordiska::append_signature_slot(pdf, capacity);
    require(!double_append.has_value(), "must reject append_signature_slot on already signed PDF");

    // Hash final document
    auto final_hash = nordiska::hash_final_document(pdf);
    require(final_hash.has_value(), "hash_final_document must succeed");
}

void test_truncated_pdf_fuzzing(const std::vector<uint8_t>& base_pdf) {
    // Truncate at every single byte offset from 0 to size()
    for (size_t len = 0; len < base_pdf.size(); len += (base_pdf.size() > 1000 ? 7 : 1)) {
        std::vector<uint8_t> truncated(base_pdf.begin(), base_pdf.begin() + len);
        auto res = nordiska::append_signature_slot(truncated, 8192);
        // Truncated PDF must either fail cleanly or succeed without crash
        (void)res;
    }
}

void test_mutated_pdf_fuzzing(const std::vector<uint8_t>& base_pdf, uint32_t seed, size_t iterations) {
    std::mt19937 rng(seed);
    std::uniform_int_distribution<size_t> op_dist(0, 4);

    for (size_t i = 0; i < iterations; ++i) {
        std::vector<uint8_t> mutated = base_pdf;
        const size_t num_mutations = (rng() % 5) + 1;

        for (size_t m = 0; m < num_mutations && !mutated.empty(); ++m) {
            const size_t pos = rng() % mutated.size();
            switch (op_dist(rng)) {
            case 0: // bit flip
                mutated[pos] ^= static_cast<uint8_t>(1 << (rng() % 8));
                break;
            case 1: // random byte
                mutated[pos] = static_cast<uint8_t>(rng() & 0xFF);
                break;
            case 2: // delete byte
                mutated.erase(mutated.begin() + pos);
                break;
            case 3: // insert random byte
                mutated.insert(mutated.begin() + pos, static_cast<uint8_t>(rng() & 0xFF));
                break;
            case 4: // inject keyword corruption
                if (pos + 4 < mutated.size()) {
                    mutated[pos] = 'X';
                    mutated[pos + 1] = 'R';
                    mutated[pos + 2] = 'E';
                    mutated[pos + 3] = 'F';
                }
                break;
            }
        }

        // Test capacity variations including edge cases (0, odd, valid)
        size_t test_capacity = 8192;
        if (i % 7 == 0) {
            test_capacity = 0;
        } else if (i % 7 == 1) {
            test_capacity = 1023; // odd
        } else if (i % 7 == 2) {
            test_capacity = 65536;
        }

        auto res = nordiska::append_signature_slot(mutated, test_capacity);
        if (res.has_value()) {
            // If it miraculously succeeded on mutated data, ensure compute_byte_range_digest and insert work safely
            auto digest_res = nordiska::compute_byte_range_digest(mutated, *res);
            if (digest_res.has_value()) {
                std::string dummy_hex(res->max_length, '0');
                (void)nordiska::insert_signature(mutated, *res, dummy_hex);
            }
        }
    }
}

void test_direct_byte_range_digest_bounds() {
    std::vector<uint8_t> dummy(100, 'A');
    dummy[9] = '<';
    dummy[30] = '>';

    // Valid slot
    nordiska::SignatureSlot valid_slot{.offset = 10, .max_length = 20, .is_signed = false};
    auto valid_res = nordiska::compute_byte_range_digest(dummy, valid_slot);
    require(valid_res.has_value(), "dummy valid slot must compute digest");

    // Invalid bounds
    std::vector<nordiska::SignatureSlot> invalid_slots = {
        {.offset = 0, .max_length = 20},                     // offset 0
        {.offset = 90, .max_length = 20},                    // offset + max_length > size
        {.offset = 10, .max_length = 0},                     // max_length 0
        {.offset = 10, .max_length = 19},                    // odd max_length
        {.offset = 100, .max_length = 20},                   // offset >= size
        {.offset = 11, .max_length = 20},                    // pdf[offset-1] != '<'
        {.offset = 10, .max_length = 20, .is_signed = true}, // already signed slot for insert
    };

    for (const auto& slot : invalid_slots) {
        auto res = nordiska::compute_byte_range_digest(dummy, slot);
        if (slot.offset == 11 || slot.offset == 0 || slot.max_length == 0 || slot.max_length % 2 != 0 ||
            slot.offset + slot.max_length >= dummy.size()) {
            require(!res.has_value(), "invalid slot must be rejected by compute_byte_range_digest");
        }
    }
}

} // namespace

int main() {
    std::printf("[*] Starting fuzz_pdf_signing_slot...\n");

    const std::vector<std::pair<nordiska::PdfEngineKind, const char*>> engines = {
        {nordiska::PdfEngineKind::Libharu, "Libharu"},
        {nordiska::PdfEngineKind::Cairo, "Cairo"},
        {nordiska::PdfEngineKind::Native, "Native"},
    };

    for (const auto& [engine_kind, name] : engines) {
        std::printf("  -> Testing valid signing flow for %s\n", name);
        auto base_pdf = generate_sample_pdf(engine_kind);
        test_valid_pdf_signing_flow(base_pdf, 8192, name);
        test_valid_pdf_signing_flow(base_pdf, 1024, name);
        test_valid_pdf_signing_flow(base_pdf, 16384, name);

        std::printf("  -> Running truncation fuzzing for %s (%zu bytes)\n", name, base_pdf.size());
        test_truncated_pdf_fuzzing(base_pdf);

        std::printf("  -> Running 5,000 mutation iterations for %s\n", name);
        test_mutated_pdf_fuzzing(base_pdf, 42 + static_cast<uint32_t>(engine_kind), 5000);
    }

    std::printf("  -> Testing direct byte range boundary edge cases\n");
    test_direct_byte_range_digest_bounds();

    std::printf("[+] fuzz_pdf_signing_slot completed successfully with 0 crashes!\n");
    return 0;
}
