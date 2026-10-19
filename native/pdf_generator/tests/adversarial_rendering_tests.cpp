#include "nordiska/application/pdf_generator.hpp"
#include "nordiska/domain/document_types.hpp"
#include "nordiska/layout/layout_builder.hpp"
#include "nordiska/rendering/pdf_engine.hpp"
#include "pdf_test_helpers.hpp"

#include <algorithm>
#include <cstdint>
#include <iostream>
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

} // namespace

int main() {
    std::cout << "Starting adversarial rendering tests..." << std::endl;

    test_engine_valid_pdf_generation(nordiska::PdfEngineKind::Libharu, "Libharu");
    test_engine_valid_pdf_generation(nordiska::PdfEngineKind::Native, "Native");
    test_engine_valid_pdf_generation(nordiska::PdfEngineKind::Cairo, "Cairo");

    test_pdf_injection_and_escaping();

    std::cout << "All adversarial rendering tests passed successfully!" << std::endl;
    return 0;
}
