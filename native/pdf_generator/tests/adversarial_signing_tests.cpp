#include "nordiska/application/pdf_generator.hpp"
#include "nordiska/rendering/pdf_engine.hpp"
#include "nordiska/signing/pdf_signer.hpp"
#include "nordiska/signing/signature_slot_appender.hpp"
#include "pdf_test_helpers.hpp"

#include <cstdint>
#include <iostream>
#include <openssl/sha.h>
#include <span>
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

void test_signature_slot_on_malformed_pdfs() {
    std::cout << "Testing signature slot appender on malformed PDFs..." << std::endl;

    const size_t capacity = 8192; // 8 KB hex slot

    // 1. Empty buffer
    {
        std::vector<uint8_t> empty;
        auto res = nordiska::append_signature_slot(empty, capacity);
        require(!res.has_value(), "Empty buffer must be rejected");
    }

    // 2. Non-PDF garbage
    {
        std::vector<uint8_t> garbage = {'H', 'e', 'l', 'l', 'o', ' ', 'W', 'o', 'r', 'l', 'd'};
        auto res = nordiska::append_signature_slot(garbage, capacity);
        require(!res.has_value(), "Garbage text must be rejected as missing PDF header");
        require(res.error().code == nordiska::DiagnosticCode::MissingPDFHeader, "Error should mention missing header");
    }

    // 3. Header present, but missing startxref
    {
        std::string no_startxref = "%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\n%%EOF\n";
        std::vector<uint8_t> bytes(no_startxref.begin(), no_startxref.end());
        auto res = nordiska::append_signature_slot(bytes, capacity);
        require(!res.has_value(), "Missing startxref must fail");
        require(res.error().code == nordiska::DiagnosticCode::MissingStartxref, "Error should mention startxref");
    }

    // 4. Invalid startxref offset (pointing past EOF)
    {
        std::string bad_offset = "%PDF-1.4\nstartxref\n999999\n%%EOF\n";
        std::vector<uint8_t> bytes(bad_offset.begin(), bad_offset.end());
        auto res = nordiska::append_signature_slot(bytes, capacity);
        require(!res.has_value(), "startxref pointing past EOF must fail");
    }

    // 5. Bytes after %%EOF
    {
        std::string trailing = "%PDF-1.4\nxref\n0 1\n0000000000 65535 f \ntrailer\n<< /Size 1 /Root 1 0 R "
                               ">>\nstartxref\n9\n%%EOF\nEXTRA_GARBAGE";
        std::vector<uint8_t> bytes(trailing.begin(), trailing.end());
        auto res = nordiska::append_signature_slot(bytes, capacity);
        require(!res.has_value(), "Trailing bytes after EOF must fail");
        require(res.error().code == nordiska::DiagnosticCode::UnexpectedBytesAfterPDFEOF,
                "Error should mention unexpected bytes");
    }

    // 6. Missing trailer
    {
        std::string no_trailer = "%PDF-1.4\nxref\n0 1\n0000000000 65535 f \nstartxref\n9\n%%EOF\n";
        std::vector<uint8_t> bytes(no_trailer.begin(), no_trailer.end());
        auto res = nordiska::append_signature_slot(bytes, capacity);
        require(!res.has_value(), "Missing trailer must fail");
    }

    // 7. Trailer missing /Size or /Root
    {
        std::string missing_size =
            "%PDF-1.4\nxref\n0 1\n0000000000 65535 f \ntrailer\n<< /Root 1 0 R >>\nstartxref\n9\n%%EOF\n";
        std::vector<uint8_t> bytes(missing_size.begin(), missing_size.end());
        auto res = nordiska::append_signature_slot(bytes, capacity);
        require(!res.has_value(), "Trailer missing /Size must fail");
    }

    // 8. Catalog already contains /AcroForm (double signing slot prevention)
    {
        std::string existing_acro = "%PDF-1.4\n1 0 obj\n<< /Type /Catalog /Pages 2 0 R /AcroForm << >> >>\nendobj\n"
                                    "xref\n0 2\n0000000000 65535 f \n0000000009 00000 n \n"
                                    "trailer\n<< /Size 2 /Root 1 0 R >>\nstartxref\n70\n%%EOF\n";
        std::vector<uint8_t> bytes(existing_acro.begin(), existing_acro.end());
        auto res = nordiska::append_signature_slot(bytes, capacity);
        require(!res.has_value(), "Existing AcroForm must be rejected to prevent corrupting signatures");
    }
}

void test_digest_and_insert_signature_boundaries() {
    std::cout << "Testing digest computation and signature insertion boundaries..." << std::endl;

    std::vector<uint8_t> dummy_pdf = {'%', 'P', 'D', 'F', '-', '1', '.', '4', '\n'};
    dummy_pdf.resize(200, 'A');

    // 1. Invalid slot bounds for digest computation
    nordiska::SignatureSlot bad_slot_1{.offset = 0, .max_length = 100};
    require(!nordiska::compute_byte_range_digest(dummy_pdf, bad_slot_1).has_value(),
            "Offset 0 must be invalid slot bound");

    nordiska::SignatureSlot bad_slot_2{.offset = 150, .max_length = 100}; // 150 + 100 + 1 > 200
    require(!nordiska::compute_byte_range_digest(dummy_pdf, bad_slot_2).has_value(),
            "Slot exceeding pdf size must be invalid");

    // 2. Invalid signature insertion
    dummy_pdf[49] = '<';
    dummy_pdf[50 + 32] = '>';
    nordiska::SignatureSlot valid_slot{.offset = 50, .max_length = 32, .is_signed = false};

    // Odd-length hex string
    require(!nordiska::insert_signature(dummy_pdf, valid_slot, "12345").has_value(),
            "Odd-length hex string must be rejected");

    // Non-hex string
    require(!nordiska::insert_signature(dummy_pdf, valid_slot, "1234567g").has_value(),
            "Non-hex string must be rejected");

    // Empty signature
    require(!nordiska::insert_signature(dummy_pdf, valid_slot, "").has_value(),
            "Empty signature string must be rejected");

    // Signature exceeding capacity
    require(!nordiska::insert_signature(dummy_pdf, valid_slot, std::string(34, 'a')).has_value(),
            "Signature exceeding capacity must be rejected");

    // Successful insertion
    require(nordiska::insert_signature(dummy_pdf, valid_slot, "0123456789abcdef").has_value(),
            "Valid hex signature must be inserted successfully");
    require(valid_slot.is_signed, "Slot must be marked signed");

    // Attempting to insert twice
    require(!nordiska::insert_signature(dummy_pdf, valid_slot, "0123456789abcdef").has_value(),
            "Inserting into already signed slot must be rejected");
}

void test_end_to_end_signing_and_validation(nordiska::PdfEngineKind engine_kind, const char* engine_name) {
    std::cout << "Testing end-to-end signing and validation with " << engine_name << "..." << std::endl;

    nordiska::AccountStatement stmt;
    stmt.title = "Signerat Kontoutdrag";
    stmt.account_number = "SE123456";
    stmt.period = "2026-01-01 - 2026-01-31";
    stmt.closing_balance = "100 000,00 SEK";
    stmt.transactions.push_back(nordiska::StatementTransaction{
        .date = "2026-01-15",
        .description = "Lön",
        .currency = "SEK",
        .amount_minor = 3500000,
        .amount_display = "+35 000,00 SEK",
        .balance_after_display = "100 000,00 SEK",
    });

    auto layout_res = nordiska::LayoutBuilder::build_statement(stmt);
    require(layout_res.has_value(), "Layout must succeed");

    constexpr size_t kSlotCapacity = 8192;
    nordiska::PdfEngine engine(engine_kind, true);
    auto render_res = engine.render(*layout_res, kSlotCapacity + nordiska::kSignatureUpdateOverhead);
    require(render_res.has_value(), "Rendering must succeed");

    std::vector<uint8_t> pdf_bytes = std::move(*render_res);
    require(nordiska::test::is_valid_pdf_structure(pdf_bytes), "Pre-signed PDF must have valid structure");

    // Append signature slot
    auto slot_res = nordiska::append_signature_slot(pdf_bytes, kSlotCapacity);
    require(slot_res.has_value(), "Appending signature slot must succeed");
    auto slot = *slot_res;

    // Compute byte range digest
    auto digest_res = nordiska::compute_byte_range_digest(pdf_bytes, slot);
    require(digest_res.has_value(), "Byte range digest computation must succeed");

    // Insert dummy valid hex signature (e.g. 512 hex characters)
    std::string mock_signature_hex(512, 'a');
    auto insert_res = nordiska::insert_signature(pdf_bytes, slot, mock_signature_hex);
    require(insert_res.has_value(), "Inserting valid signature must succeed");

    // Verify final signed PDF structure and Ghostscript validity!
    require(nordiska::test::is_valid_pdf_structure(pdf_bytes), "Signed PDF must have valid structure");
    require(nordiska::test::validate_pdf_with_ghostscript(pdf_bytes),
            "Signed PDF must pass Ghostscript syntax and page processing validation");

    // Final document SHA-256 hash must succeed
    auto hash_res = nordiska::hash_final_document(pdf_bytes);
    require(hash_res.has_value(), "Final document hash must succeed");
}

} // namespace

int main() {
    std::cout << "Starting adversarial signing tests..." << std::endl;

    test_signature_slot_on_malformed_pdfs();
    test_digest_and_insert_signature_boundaries();

    test_end_to_end_signing_and_validation(nordiska::PdfEngineKind::Native, "Native");
    test_end_to_end_signing_and_validation(nordiska::PdfEngineKind::Libharu, "Libharu");
    test_end_to_end_signing_and_validation(nordiska::PdfEngineKind::Cairo, "Cairo");

    std::cout << "All adversarial signing tests passed successfully!" << std::endl;
    return 0;
}
