#include "nordiska/signing/signature_slot_appender.hpp"

#include <algorithm>
#include <charconv>
#include <chrono>
#include <format>
#include <memory>
#include <openssl/evp.h>
#include <optional>
#include <string>

namespace nordiska {
namespace {

constexpr size_t kOffsetWidth = 10;
constexpr uint64_t kMaxPdfOffset = 9'999'999'999;

bool is_space(char c) {
    return c == ' ' || c == '\t' || c == '\r' || c == '\n' || c == '\f' || c == '\0';
}

void skip_space(std::string_view& text) {
    while (!text.empty() && is_space(text.front())) {
        text.remove_prefix(1);
    }
}

bool consume(std::string_view& text, std::string_view token) {
    skip_space(text);
    if (!text.starts_with(token)) {
        return false;
    }
    text.remove_prefix(token.size());
    return true;
}

std::optional<size_t> read_number(std::string_view& text) {
    skip_space(text);
    size_t value{};
    const auto [end, error] = std::from_chars(text.data(), text.data() + text.size(), value);
    if (error != std::errc{} || (end != text.data() + text.size() && !is_space(*end) && *end != '>')) {
        return std::nullopt;
    }
    text.remove_prefix(static_cast<size_t>(end - text.data()));
    return value;
}

std::optional<std::string_view> after_key(std::string_view dictionary, std::string_view key) {
    size_t start = 0;
    while ((start = dictionary.find(key, start)) != std::string_view::npos) {
        const auto end = start + key.size();
        if (end < dictionary.size() && (is_space(dictionary[end]) || dictionary[end] == '/' || dictionary[end] == '[' ||
                                        dictionary[end] == '<' || dictionary[end] == '(')) {
            return dictionary.substr(end);
        }
        start = end;
    }
    return std::nullopt;
}

struct PdfStructure {
    size_t xref_offset;
    size_t object_count;
    size_t root_id;
    std::string catalog_body;
    std::string trailer_body;
};

std::expected<PdfStructure, SigningError> inspect_pdf(std::span<const uint8_t> bytes,
                                                      SignaturePreparationTiming* timing) {
    auto previous = timing ? std::chrono::steady_clock::now() : std::chrono::steady_clock::time_point{};
    const auto checkpoint = [&](double SignaturePreparationTiming::*field) {
        if (timing) {
            const auto now = std::chrono::steady_clock::now();
            timing->*field += std::chrono::duration<double>(now - previous).count();
            previous = now;
        }
    };
    const auto invalid = [](std::string message) {
        return std::unexpected(SigningError{SigningErrorKind::InvalidPdf, std::move(message)});
    };
    const std::string_view pdf(reinterpret_cast<const char*>(bytes.data()), bytes.size());
    if (!pdf.starts_with("%PDF-")) {
        return invalid("Missing PDF header");
    }
    const auto start = pdf.rfind("startxref");
    if (start == std::string_view::npos) {
        return invalid("Missing startxref");
    }
    auto ending = pdf.substr(start + 9);
    const auto xref_offset = read_number(ending);
    if (!xref_offset || *xref_offset >= start || !consume(ending, "%%EOF")) {
        return invalid("Invalid startxref or EOF");
    }
    skip_space(ending);
    if (!ending.empty()) {
        return invalid("Unexpected bytes after PDF EOF");
    }
    auto xref = pdf.substr(*xref_offset, start - *xref_offset);
    if (!consume(xref, "xref")) {
        return invalid("Only classic cross-reference tables from our renderers are supported");
    }
    checkpoint(&SignaturePreparationTiming::locate_xref_seconds);
    const auto trailer_pos = xref.find("trailer");
    if (trailer_pos == std::string_view::npos) {
        return invalid("Missing trailer");
    }
    auto trailer = xref.substr(trailer_pos + 7);
    skip_space(trailer);
    const auto trailer_end = trailer.rfind(">>");
    if (!trailer.starts_with("<<") || trailer_end == std::string_view::npos) {
        return invalid("Invalid trailer dictionary");
    }
    trailer = trailer.substr(2, trailer_end - 2);
    if (after_key(trailer, "/Prev") || after_key(trailer, "/Encrypt") || after_key(trailer, "/XRefStm")) {
        return invalid("Existing revisions, encryption and hybrid xrefs are unsupported");
    }
    auto size_text = after_key(trailer, "/Size");
    auto root_text = after_key(trailer, "/Root");
    if (!size_text || !root_text) {
        return invalid("Trailer must contain /Size and /Root");
    }
    const auto count = read_number(*size_text);
    const auto root = read_number(*root_text);
    const auto generation = read_number(*root_text);
    if (!count || !root || !generation || *generation != 0 || !consume(*root_text, "R") || *root == 0 ||
        *root >= *count || *count > kMaxPdfOffset - 2) {
        return invalid("Invalid trailer object references");
    }

    checkpoint(&SignaturePreparationTiming::trailer_seconds);
    auto table = xref.substr(0, trailer_pos);
    std::optional<size_t> root_offset;
    skip_space(table);
    while (!table.empty()) {
        const auto first = read_number(table);
        const auto length = read_number(table);
        if (!first || !length || *first >= *count || *length > *count - *first) {
            return invalid("Invalid xref subsection");
        }
        for (size_t i = 0; i < *length; ++i) {
            const auto offset = read_number(table);
            const auto gen = read_number(table);
            skip_space(table);
            if (!offset || !gen || table.empty() || (table.front() != 'n' && table.front() != 'f')) {
                return invalid("Invalid xref entry");
            }
            if (*first + i == *root && table.front() == 'n' && *gen == 0) {
                root_offset = *offset;
            }
            table.remove_prefix(1);
        }
        skip_space(table);
    }
    if (!root_offset || *root_offset >= *xref_offset) {
        return invalid("Catalog missing from xref");
    }
    checkpoint(&SignaturePreparationTiming::xref_entries_seconds);
    auto catalog = pdf.substr(*root_offset, *xref_offset - *root_offset);
    const auto catalog_id = read_number(catalog);
    const auto catalog_gen = read_number(catalog);
    if (catalog_id != root || catalog_gen != generation || !consume(catalog, "obj")) {
        return invalid("Catalog xref does not point to the root object");
    }
    skip_space(catalog);
    const auto object_end = catalog.find("endobj");
    if (object_end == std::string_view::npos) {
        return invalid("Unterminated catalog object");
    }
    catalog = catalog.substr(0, object_end);
    const auto catalog_end = catalog.rfind(">>");
    if (!catalog.starts_with("<<") || catalog_end == std::string_view::npos) {
        return invalid("Invalid catalog dictionary");
    }
    catalog = catalog.substr(2, catalog_end - 2);
    auto type = after_key(catalog, "/Type");
    if (!type || !consume(*type, "/Catalog") || !after_key(catalog, "/Pages")) {
        return invalid("Root is not a page catalog");
    }
    if (after_key(catalog, "/AcroForm") || after_key(catalog, "/Version")) {
        return invalid("Catalog already contains a form or version override");
    }

    checkpoint(&SignaturePreparationTiming::catalog_seconds);
    // Preserve existing trailer metadata (/Info, /ID) while replacing /Size below.
    const auto size_position = trailer.size() - after_key(trailer, "/Size")->size();
    auto size_value = trailer.substr(size_position);
    skip_space(size_value);
    const auto digits_start = trailer.size() - size_value.size();
    (void)read_number(size_value);
    const auto digits_end = trailer.size() - size_value.size();
    std::string trailer_body(trailer);
    trailer_body.replace(digits_start, digits_end - digits_start, std::to_string(*count + 2));
    PdfStructure result{*xref_offset, *count, *root, std::string(catalog), std::move(trailer_body)};
    checkpoint(&SignaturePreparationTiming::metadata_copy_seconds);
    return result;
}

bool valid_slot(std::span<const uint8_t> pdf, const SignatureSlot& slot) {
    return slot.offset > 0 && slot.offset < pdf.size() && slot.max_length > 0 && slot.max_length % 2 == 0 &&
           slot.max_length < pdf.size() - slot.offset && pdf[slot.offset - 1] == '<' &&
           pdf[slot.offset + slot.max_length] == '>';
}

std::expected<Sha256Digest, SigningError> hash_ranges(std::span<const uint8_t> first,
                                                      std::span<const uint8_t> second = {}) {
    const std::unique_ptr<EVP_MD_CTX, decltype(&EVP_MD_CTX_free)> context(EVP_MD_CTX_new(), EVP_MD_CTX_free);
    Sha256Digest digest{};
    unsigned int length{};
    if (!context || EVP_DigestInit_ex(context.get(), EVP_sha256(), nullptr) != 1 ||
        (!first.empty() && EVP_DigestUpdate(context.get(), first.data(), first.size()) != 1) ||
        (!second.empty() && EVP_DigestUpdate(context.get(), second.data(), second.size()) != 1) ||
        EVP_DigestFinal_ex(context.get(), digest.data(), &length) != 1 || length != digest.size()) {
        return std::unexpected(SigningError{SigningErrorKind::DigestFailed, "OpenSSL SHA-256 digest failed"});
    }
    return digest;
}

} // namespace

std::expected<SignatureSlot, SigningError> append_signature_slot(std::vector<uint8_t>& pdf, size_t capacity,
                                                                 SignaturePreparationTiming* timing) {
    if (capacity == 0 || capacity % 2 != 0) {
        return std::unexpected(SigningError{SigningErrorKind::InvalidArgument,
                                            "Signature capacity must be a positive even number of hex characters"});
    }
    if (pdf.size() > kMaxPdfOffset || capacity > kMaxPdfOffset - pdf.size() || capacity > pdf.max_size() - pdf.size()) {
        return std::unexpected(
            SigningError{SigningErrorKind::ResourceLimitExceeded, "PDF signature capacity too large"});
    }
    auto structure = inspect_pdf(pdf, timing);
    if (!structure) {
        return std::unexpected(structure.error());
    }
    const auto format_start = timing ? std::chrono::steady_clock::now() : std::chrono::steady_clock::time_point{};
    const auto& [previous_xref, object_count, root_id, catalog_body, trailer_body] = *structure;
    const size_t field_id = object_count;
    const size_t signature_id = object_count + 1;
    const size_t root_offset = pdf.size() + 1;
    // An invisible signature field belongs in AcroForm/Fields. It needs no page widget.
    std::string prefix =
        std::format("\n{} 0 obj\n<<{}\n/Version /1.7\n/AcroForm << /SigFlags 3 /Fields [ {} 0 R ] >>\n>>\nendobj\n",
                    root_id, catalog_body, field_id);
    const size_t field_offset = pdf.size() + prefix.size();
    prefix += std::format("{} 0 obj\n<< /FT /Sig /T (Signature1) /V {} 0 R >>\nendobj\n", field_id, signature_id);
    const size_t signature_offset = pdf.size() + prefix.size();
    prefix += std::format(
        "{} 0 obj\n<< /Type /Sig /Filter /Adobe.PPKLite /SubFilter /ETSI.CAdES.detached\n/ByteRange [", signature_id);
    const size_t range_position = prefix.size();
    prefix += "0000000000 0000000000 0000000000 0000000000]\n/Contents <";
    const size_t slot_offset = pdf.size() + prefix.size();
    const std::string_view closer = ">\n>>\nendobj\n";
    const size_t xref_offset = slot_offset + capacity + closer.size();
    const auto suffix =
        std::format("{}xref\n0 1\n0000000000 65535 f \n{} 1\n{:010} 00000 n \n{} 2\n{:010} 00000 n \n{:010} 00000 n \n"
                    "trailer\n<<{}\n/Prev {}\n>>\nstartxref\n{}\n%%EOF\n",
                    closer, root_id, root_offset, field_id, field_offset, signature_offset, trailer_body, previous_xref,
                    xref_offset);
    const size_t final_size = slot_offset + capacity + suffix.size();
    if (final_size > kMaxPdfOffset || final_size > pdf.max_size()) {
        return std::unexpected(
            SigningError{SigningErrorKind::ResourceLimitExceeded, "PDF exceeds 10-digit xref offsets"});
    }
    const size_t second_offset = slot_offset + capacity + 1;
    const std::array<size_t, 4> ranges{0, slot_offset - 1, second_offset, final_size - second_offset};
    for (size_t i = 0; i < ranges.size(); ++i) {
        const auto number = std::format("{:010}", ranges[i]);
        prefix.replace(range_position + i * (kOffsetWidth + 1), kOffsetWidth, number);
    }
    // For our renderers the tail is already reserved. The fallback also permits
    // direct helper callers without relying on an unchecked capacity promise.
    const auto write_start = timing ? std::chrono::steady_clock::now() : std::chrono::steady_clock::time_point{};
    if (timing) {
        timing->format_seconds += std::chrono::duration<double>(write_start - format_start).count();
    }
    pdf.reserve(final_size);
    pdf.insert(pdf.end(), prefix.begin(), prefix.end());
    pdf.resize(pdf.size() + capacity, '0');
    pdf.insert(pdf.end(), suffix.begin(), suffix.end());
    if (timing) {
        timing->buffer_write_seconds +=
            std::chrono::duration<double>(std::chrono::steady_clock::now() - write_start).count();
    }
    return SignatureSlot{.offset = slot_offset, .max_length = capacity, .is_signed = false};
}

std::expected<Sha256Digest, SigningError> compute_byte_range_digest(std::span<const uint8_t> pdf,
                                                                    const SignatureSlot& slot) {
    if (!valid_slot(pdf, slot)) {
        return std::unexpected(SigningError{SigningErrorKind::InvalidArgument, "Invalid signature slot bounds"});
    }
    return hash_ranges(pdf.first(slot.offset - 1), pdf.subspan(slot.offset + slot.max_length + 1));
}

std::expected<Sha256Digest, SigningError> hash_final_document(std::span<const uint8_t> pdf) {
    return hash_ranges(pdf);
}

std::expected<void, SigningError> insert_signature(std::span<uint8_t> pdf, SignatureSlot& slot, std::string_view hex) {
    if (!valid_slot(pdf, slot) || slot.is_signed) {
        return std::unexpected(
            SigningError{SigningErrorKind::InvalidArgument, "Invalid or already filled signature slot"});
    }
    if (hex.size() > slot.max_length) {
        return std::unexpected(
            SigningError{SigningErrorKind::SignatureTooLarge, "CMS hex exceeds reserved /Contents capacity"});
    }
    if (hex.empty() || hex.size() % 2 != 0 || !std::ranges::all_of(hex, [](char c) {
            return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
        })) {
        return std::unexpected(
            SigningError{SigningErrorKind::InvalidSignatureOutput, "Signer returned invalid CMS hex"});
    }
    std::ranges::copy(hex, pdf.begin() + slot.offset);
    std::fill(pdf.begin() + slot.offset + hex.size(), pdf.begin() + slot.offset + slot.max_length, '0');
    slot.is_signed = true; // Embedded successfully; does not assert certificate trust or CMS validity.
    return {};
}

} // namespace nordiska
