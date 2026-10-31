#include "nordiska/application/pdf_generator.hpp"
#include "nordiska/domain/document_types.hpp"
#include "nordiska/layout/layout_builder.hpp"
#include "nordiska/rendering/cairo_pdf_engine.hpp"
#include "nordiska/rendering/haru_pdf_engine.hpp"
#include "nordiska/rendering/pdf_engine.hpp"
#include "pdf_test_helpers.hpp"

#include <algorithm>
#include <cstdint>
#include <cstdlib>
#include <iostream>
#include <optional>
#include <stdexcept>
#include <string>
#include <vector>

namespace {

void require(bool condition, const char* message) {
    if (!condition) {
        std::cerr << "Assertion failed: " << message << std::endl;
        throw std::runtime_error(message);
    }
}

class ScopedEnv {
  public:
    ScopedEnv(const char* name, const char* value) : name_(name) {
        if (const char* old = std::getenv(name)) {
            old_ = old;
        }
        set(value);
    }
    ~ScopedEnv() {
        set(old_ ? old_->c_str() : nullptr);
    }
    void set(const char* val) const {
        if (val) {
            ::setenv(name_, val, 1);
        } else {
            ::unsetenv(name_);
        }
    }

  private:
    const char* name_;
    std::optional<std::string> old_;
};

nordiska::AccountStatement create_statement_with_transactions(size_t tx_count, const std::string& desc_prefix = "Tx ") {
    nordiska::AccountStatement stmt;
    stmt.title = "Kontoutdrag - Stresstest";
    stmt.account_number = "SE550000000012345678";
    stmt.account_name = "Kapitalkonto";
    stmt.currency = "SEK";
    stmt.period = "2026-01-01 - 2026-12-31";
    stmt.opening_balance = "1 000 000,00 SEK";
    stmt.closing_balance = "2 500 000,00 SEK";

    stmt.transactions.reserve(tx_count);
    for (size_t i = 0; i < tx_count; ++i) {
        stmt.transactions.push_back(nordiska::StatementTransaction{
            .date = "2026-06-15",
            .type = (i % 2 == 0) ? "Kortköp" : "Överföring",
            .description = desc_prefix + std::to_string(i + 1),
            .currency = "SEK",
            .amount_minor = static_cast<int64_t>((i + 1) * 100),
            .amount_display = (i % 2 == 0) ? "−150,00 SEK" : "+2 500,00 SEK",
            .balance_after_display = "1 500 000,00 SEK",
        });
    }
    return stmt;
}

void test_engine_valid_pdf_generation(nordiska::PdfEngineKind engine_kind, const char* engine_name) {
    std::cout << "Testing engine valid PDF generation: " << engine_name << "..." << std::endl;

    // 1. Empty statement (0 transactions)
    {
        auto stmt = create_statement_with_transactions(0);
        auto layout_res = nordiska::LayoutBuilder::build_statement(stmt);
        require(layout_res.has_value(), "Layout building for 0 transactions must succeed");

        nordiska::PdfEngine engine(engine_kind, true);
        auto pdf_res = engine.render(*layout_res);
        require(pdf_res.has_value(), "Rendering 0 transactions must succeed");
        require(nordiska::test::is_valid_pdf_structure(*pdf_res), "0 transactions PDF must have valid structure");
        require(nordiska::test::validate_pdf_with_ghostscript(*pdf_res),
                "0 transactions PDF must pass Ghostscript validation");
    }

    // 2. Medium statement (100 transactions, ~4 pages)
    {
        auto stmt = create_statement_with_transactions(100);
        auto layout_res = nordiska::LayoutBuilder::build_statement(stmt);
        require(layout_res.has_value(), "Layout building for 100 transactions must succeed");
        require(layout_res->pages.size() >= 3, "100 transactions should span multiple pages");

        nordiska::PdfEngine engine_comp(engine_kind, true);
        auto pdf_comp_res = engine_comp.render(*layout_res);
        require(pdf_comp_res.has_value(), "Rendering compressed medium statement must succeed");
        require(nordiska::test::is_valid_pdf_structure(*pdf_comp_res), "Medium PDF must have valid structure");
        require(nordiska::test::validate_pdf_with_ghostscript(*pdf_comp_res),
                "Medium PDF must pass Ghostscript validation");

        nordiska::PdfEngine engine_uncomp(engine_kind, false);
        auto pdf_uncomp_res = engine_uncomp.render(*layout_res);
        require(pdf_uncomp_res.has_value(), "Rendering uncompressed medium statement must succeed");
        require(nordiska::test::is_valid_pdf_structure(*pdf_uncomp_res),
                "Uncompressed medium PDF must have valid structure");
        require(nordiska::test::validate_pdf_with_ghostscript(*pdf_uncomp_res),
                "Uncompressed medium PDF must pass Ghostscript validation");
    }

    // 3. Massive statement (4,000 transactions, ~112 pages)
    // Stresses memory allocation, page count limits, and bump arena boundaries.
    {
        auto stmt = create_statement_with_transactions(4000);
        auto layout_res = nordiska::LayoutBuilder::build_statement(stmt);
        require(layout_res.has_value(), "Layout building for 4000 transactions must succeed");
        require(layout_res->pages.size() > 100, "4000 transactions must produce > 100 pages");

        nordiska::PdfEngine engine(engine_kind, true);
        auto pdf_res = engine.render(*layout_res);
        require(pdf_res.has_value(), "Rendering 4000 transactions must succeed without error");
        require(nordiska::test::is_valid_pdf_structure(*pdf_res), "Massive PDF must have valid structure");
        require(nordiska::test::validate_pdf_with_ghostscript(*pdf_res),
                "Massive PDF must pass Ghostscript validation");
    }

    // 4. Annual Tax Report
    {
        nordiska::AnnualTaxReport tax{
            .title = "Kontrolluppgift för ränteinkomst",
            .tax_year = "2025",
            .account_number = "NKM-99887766",
            .account_name = "Fasträntekonto",
            .total_interest_earned = "15 420,50 SEK",
            .preliminary_tax_deducted = "4 626,15 SEK",
            .reported_to_authority = "Skatteverket (KU20)",
        };
        auto layout_res = nordiska::LayoutBuilder::build_tax_report(tax);
        require(layout_res.has_value(), "Tax report layout must succeed");

        nordiska::PdfEngine engine(engine_kind, true);
        auto pdf_res = engine.render(*layout_res);
        require(pdf_res.has_value(), "Tax report rendering must succeed");
        require(nordiska::test::is_valid_pdf_structure(*pdf_res), "Tax report PDF must have valid structure");
        require(nordiska::test::validate_pdf_with_ghostscript(*pdf_res),
                "Tax report PDF must pass Ghostscript validation");
    }
}

void test_pdf_injection_and_escaping() {
    std::cout << "Testing PDF operator injection and string escaping..." << std::endl;

    // Test adversarial strings meant to break PDF literal string syntax or inject operators
    std::vector<std::string> adversarial_strings = {
        ") Tj /F1 100 Tf (malicious injection",
        "nested ((parentheses)) (and) ((more))",
        "unmatched ) single close parenthesis",
        "unmatched ( single open parenthesis",
        "trailing backslash \\",
        "multiple backslashes \\\\\\\\ and \\( \\)",
        std::string("null byte in text \0 after null", 30),
        "line break \r\n carriage return \r newline \n tab \t",
        "Swedish characters: Åke & Älva köpte öl för 125 kr på Österlen",
        "Unicode typography: ”Nordiska” – ’citat’ … 100 €",
        "Unicode minus: −999 999,99 SEK vs hyphen: -999",
        "Zero width & BOM: \xEF\xBB\xBF"
        "Hello\xE2\x80\x8BWorld",
        "Unmapped Unicode: Cyrillic Привет, Arabic مرحبا, Emoji 🚀🔥, CJK 銀行",
        std::string(2000, 'A'), // Extremely long string without spaces
    };

    for (const auto& adv_str : adversarial_strings) {
        nordiska::AccountStatement stmt;
        stmt.title = "Test: " + adv_str;
        stmt.account_number = "SE123456789";
        stmt.account_name = adv_str;
        stmt.currency = "SEK";
        stmt.period = adv_str;
        stmt.opening_balance = adv_str;
        stmt.closing_balance = adv_str;
        stmt.transactions.push_back(nordiska::StatementTransaction{
            .date = "2026-01-01",
            .type = adv_str,
            .description = adv_str,
            .currency = "SEK",
            .amount_minor = 100,
            .amount_display = adv_str,
            .balance_after_display = adv_str,
        });

        auto layout_res = nordiska::LayoutBuilder::build_statement(stmt);
        require(layout_res.has_value(), "Layout building with adversarial strings must succeed");

        for (auto engine_kind :
             {nordiska::PdfEngineKind::Native, nordiska::PdfEngineKind::Libharu, nordiska::PdfEngineKind::Cairo}) {
            nordiska::PdfEngine engine(engine_kind, true);
            auto pdf_res = engine.render(*layout_res);
            require(pdf_res.has_value(), "Rendering with adversarial string must succeed");
            require(nordiska::test::is_valid_pdf_structure(*pdf_res),
                    "Adversarial string PDF must have valid structure");
            require(nordiska::test::validate_pdf_with_ghostscript(*pdf_res),
                    "Adversarial string PDF must pass Ghostscript syntax and rendering validation");
        }
    }
}

void test_ultra_long_unbroken_strings_robustness() {
    std::cout << "Testing ultra-long unbroken string robustness (500, 2000, 10000 chars)..." << std::endl;

    const std::vector<std::pair<std::string, std::string>> long_cases = {
        {"500 ASCII chars", std::string(500, 'W')},
        {"2000 ASCII chars", std::string(2000, 'X')},
        {"10000 ASCII chars", std::string(10000, 'Z')},
        {"1000 multi-byte Swedish chars",
         []() {
             std::string s;
             s.reserve(2000);
             for (int i = 0; i < 500; ++i) {
                 s += "åä";
             }
             return s;
         }()},
    };

    for (const auto& [label, str] : long_cases) {
        nordiska::AccountStatement stmt;
        stmt.title = "Statement - " + label;
        stmt.account_number = "SE9999000011112222";
        stmt.account_name = str; // ultra-long customer/account name
        stmt.currency = "SEK";
        stmt.period = "2026-01-01 - 2026-12-31";
        stmt.opening_balance = "100,00 SEK";
        stmt.closing_balance = "200,00 SEK";
        stmt.transactions.push_back(nordiska::StatementTransaction{
            .date = "2026-06-01",
            .type = "Kortköp",
            .description = str, // ultra-long unbroken description
            .currency = "SEK",
            .amount_minor = 10000,
            .amount_display = "100,00 SEK",
            .balance_after_display = "200,00 SEK",
        });

        auto layout_res = nordiska::LayoutBuilder::build_statement(stmt);
        require(layout_res.has_value(), ("Layout building for " + label + " must succeed").c_str());

        for (auto engine_kind :
             {nordiska::PdfEngineKind::Native, nordiska::PdfEngineKind::Libharu, nordiska::PdfEngineKind::Cairo}) {
            nordiska::PdfEngine engine(engine_kind, true);
            auto pdf_res = engine.render(*layout_res);
            require(pdf_res.has_value(), ("Rendering for " + label + " must succeed").c_str());
            require(nordiska::test::is_valid_pdf_structure(*pdf_res), ("Valid PDF structure for " + label).c_str());
            require(nordiska::test::validate_pdf_with_ghostscript(*pdf_res),
                    ("Ghostscript validation for " + label).c_str());
        }
    }
}

void test_cairo_font_fallbacks_and_failure() {
    std::cout << "Testing Cairo font fallback chain and forced failure handling..." << std::endl;

    auto stmt = create_statement_with_transactions(2);
    auto layout_res = nordiska::LayoutBuilder::build_statement(stmt);
    require(layout_res.has_value(), "Layout building must succeed");

    // 1. Normal Cairo rendering succeeds with standard font fallback chain (Helvetica -> Arial -> sans-serif)
    {
        nordiska::test::reload_cairo_fonts();
        nordiska::PdfEngine engine(nordiska::PdfEngineKind::Cairo, true);
        auto pdf_res = engine.render(*layout_res);
        require(pdf_res.has_value(), "Normal Cairo rendering must succeed with fallback chain");
        require(nordiska::test::is_valid_pdf_structure(*pdf_res), "Rendered PDF must have valid structure");
    }

    // 2. Simulated font resolution failure returns clean EngineError without crashing
    {
        ScopedEnv force_fail("NORDISKA_PDF_CAIRO_FONT_OVERRIDE", "__FORCE_FAILURE__");
        nordiska::test::reload_cairo_fonts();
        nordiska::PdfEngine engine(nordiska::PdfEngineKind::Cairo, true);
        auto pdf_res = engine.render(*layout_res);
        require(!pdf_res.has_value(), "Cairo rendering must fail when fonts cannot be resolved");
        require(pdf_res.error().kind == nordiska::RenderErrorKind::EngineError,
                "Failed font resolution must return EngineError");
        require(pdf_res.error().code ==
                    nordiska::DiagnosticCode::CairoFailedToResolveAValidFontFaceTriedHelveticaArialSansSerif,
                "Error message must mention font face resolution failure");
    }

    // 3. Restoring environment restores healthy Cairo rendering
    {
        nordiska::test::reload_cairo_fonts();
        nordiska::PdfEngine engine(nordiska::PdfEngineKind::Cairo, true);
        auto pdf_res = engine.render(*layout_res);
        require(pdf_res.has_value(), "Cairo rendering must recover cleanly after restoring font resolution");
    }
}

void test_haru_bump_arena_limits_and_teardown() {
    std::cout << "Testing HaruBumpArena limits, geometric growth, and memory teardown..." << std::endl;

    // Ensure clean state before starting
    nordiska::test::reset_haru_arena();
    auto stats = nordiska::test::get_haru_arena_stats();
    require(stats.base_chunk_capacity == 2 * 1024 * 1024, "Default base chunk must be 2 MB");
    require(stats.overflow_chunk_count == 0, "Initial overflow chunk count must be 0");
    require(stats.total_capacity == 2 * 1024 * 1024, "Initial total capacity must be 2 MB");
    require(!stats.budget_exceeded, "Budget exceeded flag must be false initially");

    // 1. Direct bump allocation within base chunk (2 MB)
    void* p1 = nordiska::test::haru_arena_allocate_for_test(1024 * 1024);
    require(p1 != nullptr, "1 MB allocation must succeed in base chunk");
    stats = nordiska::test::get_haru_arena_stats();
    require(stats.overflow_chunk_count == 0, "No overflow chunk should be created for 1 MB");
    require(stats.total_capacity == 2 * 1024 * 1024, "Capacity should remain 2 MB");

    // Fill the remainder of the 2 MB base chunk
    void* p2 = nordiska::test::haru_arena_allocate_for_test(1024 * 1024);
    require(p2 != nullptr, "Second 1 MB allocation should succeed in base chunk");

    // 2. Geometric growth verification:
    // Base: 2 MB
    // Overflow 1: 4 MB (Total = 6 MB)
    // Overflow 2: 6 MB (Total = 12 MB)
    // Overflow 3: 20 MB (Total = 32 MB)

    // A. Overflow into Overflow Chunk 1 (4 MB, Total = 6 MB)
    void* p_ov1 = nordiska::test::haru_arena_allocate_for_test(1);
    require(p_ov1 != nullptr, "Overflow 1 allocation must succeed");
    stats = nordiska::test::get_haru_arena_stats();
    require(stats.overflow_chunk_count == 1, "Exactly 1 overflow chunk must exist");
    require(stats.total_capacity == 6 * 1024 * 1024, "Capacity after Overflow 1 must be exactly 6 MB");

    // B. Overflow into Overflow Chunk 2 (6 MB, Total = 12 MB)
    void* p_ov2 = nordiska::test::haru_arena_allocate_for_test(4 * 1024 * 1024);
    require(p_ov2 != nullptr, "Overflow 2 allocation must succeed");
    stats = nordiska::test::get_haru_arena_stats();
    require(stats.overflow_chunk_count == 2, "Exactly 2 overflow chunks must exist");
    require(stats.total_capacity == 12 * 1024 * 1024, "Capacity after Overflow 2 must be exactly 12 MB");

    // C. Overflow into Overflow Chunk 3 (20 MB, Total = 32 MB)
    void* p_ov3 = nordiska::test::haru_arena_allocate_for_test(6 * 1024 * 1024);
    require(p_ov3 != nullptr, "Overflow 3 allocation must succeed");
    stats = nordiska::test::get_haru_arena_stats();
    require(stats.overflow_chunk_count == 3, "Exactly 3 overflow chunks must exist");
    require(stats.total_capacity == 32 * 1024 * 1024, "Capacity after Overflow 3 must reach 32 MB cap");

    // 3. Hard ceiling (32 MB cap) rejection:
    // Any allocation exceeding the 32 MB arena budget must return nullptr
    void* p_overflow = nordiska::test::haru_arena_allocate_for_test(15 * 1024 * 1024);
    require(p_overflow == nullptr, "Allocation exceeding 32 MB cap must return nullptr");
    stats = nordiska::test::get_haru_arena_stats();
    require(stats.budget_exceeded, "budget_exceeded flag must be true");
    require(stats.total_capacity == 32 * 1024 * 1024, "Total capacity must remain capped at 32 MB");

    // 4. Deterministic teardown on reset():
    // Overflow chunks must be deallocated immediately, returning to warm 2 MB baseline
    nordiska::test::reset_haru_arena();
    stats = nordiska::test::get_haru_arena_stats();
    require(stats.overflow_chunk_count == 0, "Teardown must deallocate all overflow chunks immediately");
    require(stats.base_chunk_capacity == 2 * 1024 * 1024, "Base chunk must remain warm 2 MB baseline");
    require(stats.total_capacity == 2 * 1024 * 1024, "Total capacity must return to warm 2 MB baseline");
    require(!stats.budget_exceeded, "budget_exceeded must be reset to false");

    // Post-reset allocation must work immediately in the base chunk
    void* p_after_reset = nordiska::test::haru_arena_allocate_for_test(512);
    require(p_after_reset != nullptr, "Allocation after teardown must succeed in warm base chunk");
    stats = nordiska::test::get_haru_arena_stats();
    require(stats.overflow_chunk_count == 0, "Post-reset allocation must stay in base chunk");

    // 5. Environment variable overrides:
    // A. Base chunk override via NORDISKA_PDF_HARU_BASE_CHUNK_BYTES
    {
        ScopedEnv custom_base("NORDISKA_PDF_HARU_BASE_CHUNK_BYTES", "1048576"); // 1 MB
        nordiska::test::reset_haru_arena();
        stats = nordiska::test::get_haru_arena_stats();
        require(stats.base_chunk_capacity == 1048576, "Base chunk must honor NORDISKA_PDF_HARU_BASE_CHUNK_BYTES");
        require(stats.total_capacity == 1048576, "Total capacity must equal configured base chunk");
    }
    nordiska::test::reset_haru_arena();
    stats = nordiska::test::get_haru_arena_stats();
    require(stats.base_chunk_capacity == 2 * 1024 * 1024, "Base chunk must return to 2 MB after env unset");

    // B. Hard ceiling override via NORDISKA_PDF_HARU_MAX_ARENA_BYTES and end-to-end Libharu rendering
    {
        // Constrain max arena to 256 KB so rendering a massive 4,000 tx statement fails due to OOM
        ScopedEnv tight_ceiling("NORDISKA_PDF_HARU_MAX_ARENA_BYTES", "262144");
        auto stmt = create_statement_with_transactions(4000);
        auto layout_res = nordiska::LayoutBuilder::build_statement(stmt);
        require(layout_res.has_value(), "Layout building for 4000 tx must succeed");

        nordiska::PdfEngine engine(nordiska::PdfEngineKind::Libharu, true);
        auto render_res = engine.render(*layout_res);
        require(!render_res.has_value(), "Rendering massive statement with 256 KB arena cap must fail");
        require(render_res.error().kind == nordiska::RenderErrorKind::OutOfMemory,
                "Error must be OutOfMemory when arena budget exceeded");
    }

    // C. Immediate recovery on next render once limit is restored
    {
        auto stmt = create_statement_with_transactions(100);
        auto layout_res = nordiska::LayoutBuilder::build_statement(stmt);
        require(layout_res.has_value(), "Layout building for 100 tx must succeed");

        nordiska::PdfEngine engine(nordiska::PdfEngineKind::Libharu, true);
        auto render_res = engine.render(*layout_res);
        require(render_res.has_value(), "Rendering 100 tx statement after recovery must succeed");
        require(nordiska::test::is_valid_pdf_structure(*render_res), "Recovered PDF must have valid structure");

        stats = nordiska::test::get_haru_arena_stats();
        require(!stats.budget_exceeded, "Budget exceeded must be false after successful render");
        require(stats.overflow_chunk_count == 0, "100 tx statement must fit within warm 2 MB base chunk");
    }

    // D. Geometric growth beyond 32 MB when configured via NORDISKA_PDF_HARU_MAX_ARENA_BYTES
    {
        // 128 MB ceiling (128 * 1024 * 1024 = 134217728)
        ScopedEnv large_ceiling("NORDISKA_PDF_HARU_MAX_ARENA_BYTES", "134217728");
        nordiska::test::reset_haru_arena();

        // Fill base chunk (2 MB)
        nordiska::test::haru_arena_allocate_for_test(2 * 1024 * 1024);
        // Overflow 1 (4 MB, Total = 6 MB)
        nordiska::test::haru_arena_allocate_for_test(1);
        // Overflow 2 (6 MB, Total = 12 MB)
        nordiska::test::haru_arena_allocate_for_test(4 * 1024 * 1024);
        // Overflow 3 (20 MB, Total = 32 MB)
        nordiska::test::haru_arena_allocate_for_test(6 * 1024 * 1024);
        stats = nordiska::test::get_haru_arena_stats();
        require(stats.total_capacity == 32 * 1024 * 1024, "Total capacity at Overflow 3 must be 32 MB");

        // Overflow 4 doubles previous overflow (40 MB, Total = 72 MB)
        nordiska::test::haru_arena_allocate_for_test(20 * 1024 * 1024);
        stats = nordiska::test::get_haru_arena_stats();
        require(stats.overflow_chunk_count == 4, "Overflow 4 must exist");
        require(stats.total_capacity == 72 * 1024 * 1024,
                "Total capacity after Overflow 4 must be 72 MB (geometric doubling)");

        // Overflow 5 (clamped to remaining 56 MB budget, Total = 128 MB)
        nordiska::test::haru_arena_allocate_for_test(40 * 1024 * 1024);
        stats = nordiska::test::get_haru_arena_stats();
        require(stats.overflow_chunk_count == 5, "Overflow 5 must exist");
        require(stats.total_capacity == 128 * 1024 * 1024, "Total capacity must reach 128 MB ceiling");

        // Further allocation must return nullptr
        void* p_exceed_128 = nordiska::test::haru_arena_allocate_for_test(60 * 1024 * 1024);
        require(p_exceed_128 == nullptr, "Allocation beyond 128 MB ceiling must return nullptr");
        stats = nordiska::test::get_haru_arena_stats();
        require(stats.budget_exceeded, "Budget exceeded must be true");
    }

    // E. Bad environment variable settings must safely fallback without breaking
    {
        // 1. Negative numbers
        {
            ScopedEnv neg_base("NORDISKA_PDF_HARU_BASE_CHUNK_BYTES", "-1");
            ScopedEnv neg_max("NORDISKA_PDF_HARU_MAX_ARENA_BYTES", "-500");
            nordiska::test::reset_haru_arena();
            stats = nordiska::test::get_haru_arena_stats();
            require(stats.base_chunk_capacity == 2 * 1024 * 1024, "Negative base chunk must fallback to default 2 MB");
            require(stats.total_capacity == 2 * 1024 * 1024, "Negative max arena must fallback to default 32 MB");
        }

        // 2. Zero values
        {
            ScopedEnv zero_base("NORDISKA_PDF_HARU_BASE_CHUNK_BYTES", "0");
            ScopedEnv zero_max("NORDISKA_PDF_HARU_MAX_ARENA_BYTES", "0");
            nordiska::test::reset_haru_arena();
            stats = nordiska::test::get_haru_arena_stats();
            require(stats.base_chunk_capacity == 2 * 1024 * 1024, "Zero base chunk must fallback to default 2 MB");
        }

        // 3. Gibberish / non-numeric / whitespace
        {
            ScopedEnv gibberish("NORDISKA_PDF_HARU_MAX_ARENA_BYTES", "not_a_number");
            ScopedEnv whitespace("NORDISKA_PDF_HARU_BASE_CHUNK_BYTES", "   ");
            nordiska::test::reset_haru_arena();
            stats = nordiska::test::get_haru_arena_stats();
            require(stats.base_chunk_capacity == 2 * 1024 * 1024, "Whitespace must fallback to default 2 MB");
        }

        // 4. Trailing garbage
        {
            ScopedEnv trailing("NORDISKA_PDF_HARU_BASE_CHUNK_BYTES", "1048576xyz");
            nordiska::test::reset_haru_arena();
            stats = nordiska::test::get_haru_arena_stats();
            require(stats.base_chunk_capacity == 2 * 1024 * 1024, "Trailing garbage must fallback to default 2 MB");
        }

        // 5. Overflow / extreme numbers
        {
            ScopedEnv huge("NORDISKA_PDF_HARU_MAX_ARENA_BYTES", "9999999999999999999999999999999999999999");
            nordiska::test::reset_haru_arena();
            stats = nordiska::test::get_haru_arena_stats();
            require(stats.base_chunk_capacity == 2 * 1024 * 1024, "Overflow string must fallback safely to default");
        }

        // 6. Inverted configuration: base > max
        {
            // Set base chunk to 64 MB but max arena to 16 MB
            ScopedEnv inv_base("NORDISKA_PDF_HARU_BASE_CHUNK_BYTES", "67108864");
            ScopedEnv inv_max("NORDISKA_PDF_HARU_MAX_ARENA_BYTES", "16777216");
            nordiska::test::reset_haru_arena();
            stats = nordiska::test::get_haru_arena_stats();
            require(stats.base_chunk_capacity == 16777216, "Base chunk must be safely clamped to max arena");
            require(stats.total_capacity == 16777216, "Total capacity must not exceed max arena ceiling");
        }
    }

    // Clean up
    nordiska::test::reset_haru_arena();
    std::cout << "HaruBumpArena limits and teardown tests passed successfully!" << std::endl;
}

} // namespace

int main() {
    std::cout << "Starting adversarial rendering tests..." << std::endl;

    test_haru_bump_arena_limits_and_teardown();

    test_engine_valid_pdf_generation(nordiska::PdfEngineKind::Libharu, "Libharu");
    test_engine_valid_pdf_generation(nordiska::PdfEngineKind::Native, "Native");
    test_engine_valid_pdf_generation(nordiska::PdfEngineKind::Cairo, "Cairo");

    test_pdf_injection_and_escaping();
    test_ultra_long_unbroken_strings_robustness();
    test_cairo_font_fallbacks_and_failure();

    std::cout << "All adversarial rendering tests passed successfully!" << std::endl;
    return 0;
}
