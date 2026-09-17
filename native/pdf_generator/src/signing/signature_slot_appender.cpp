#include "nordiska/signing/signature_slot_appender.hpp"

#include <algorithm>
#include <charconv>
#include <cstdint>
#include <cstring>
#include <format>
#include <string_view>

namespace nordiska {

namespace {

// Format a number as a 10-character zero-padded decimal string directly into destination
void write_10digit(uint8_t* dest, size_t value) {
    char temp[16];
    auto [ptr, ec] = std::to_chars(temp, temp + sizeof(temp), value);
    const size_t digits = ptr - temp;
    if (digits < 10) {
        std::memset(dest, '0', 10 - digits);
        std::memcpy(dest + (10 - digits), temp, digits);
    } else {
        std::memcpy(dest, temp, 10);
    }
}

// Parses the last startxref offset from a PDF buffer by scanning backwards from the end
size_t find_last_startxref(const std::vector<uint8_t>& buffer) {
    if (buffer.size() < 32) {
        return 0;
    }
    const size_t search_window = std::min<size_t>(buffer.size(), 2048);
    const std::string_view tail(reinterpret_cast<const char*>(buffer.data() + buffer.size() - search_window),
                                search_window);

    const size_t pos = tail.rfind("startxref");
    if (pos == std::string_view::npos) {
        return 0;
    }

    const size_t num_start = tail.find_first_of("0123456789", pos + 9);
    if (num_start == std::string_view::npos) {
        return 0;
    }

    size_t xref_offset = 0;
    std::from_chars(tail.data() + num_start, tail.data() + tail.size(), xref_offset);
    return xref_offset;
}

// Parses /Size from the trailer
size_t find_trailer_size(const std::vector<uint8_t>& buffer) {
    if (buffer.size() < 32) {
        return 10;
    }
    const size_t search_window = std::min<size_t>(buffer.size(), 2048);
    const std::string_view tail(reinterpret_cast<const char*>(buffer.data() + buffer.size() - search_window),
                                search_window);

    const size_t pos = tail.rfind("/Size");
    if (pos == std::string_view::npos) {
        return 10;
    }

    const size_t num_start = tail.find_first_of("0123456789", pos + 5);
    if (num_start == std::string_view::npos) {
        return 10;
    }

    size_t size_val = 0;
    std::from_chars(tail.data() + num_start, tail.data() + tail.size(), size_val);
    return size_val > 0 ? size_val : 10;
}

// Parses /Root reference from the trailer (e.g. "1 0 R")
std::string find_trailer_root(const std::vector<uint8_t>& buffer) {
    if (buffer.size() < 32) {
        return "1 0 R";
    }
    const size_t search_window = std::min<size_t>(buffer.size(), 2048);
    const std::string_view tail(reinterpret_cast<const char*>(buffer.data() + buffer.size() - search_window),
                                search_window);

    const size_t pos = tail.rfind("/Root");
    if (pos == std::string_view::npos) {
        return "1 0 R";
    }

    const size_t val_start = tail.find_first_not_of(" \t\r\n", pos + 5);
    if (val_start == std::string_view::npos) {
        return "1 0 R";
    }

    const size_t val_end = tail.find('R', val_start);
    if (val_end == std::string_view::npos) {
        return "1 0 R";
    }

    return std::string(tail.substr(val_start, (val_end - val_start) + 1));
}

} // namespace

SignatureSlot append_signature_slot(std::vector<uint8_t>& pdf_buffer) {
    const size_t orig_size = pdf_buffer.size();
    const size_t orig_xref = find_last_startxref(pdf_buffer);
    const size_t orig_obj_count = find_trailer_size(pdf_buffer);
    const std::string root_ref = find_trailer_root(pdf_buffer);

    const size_t annot_id = orig_obj_count;
    const size_t sig_id = orig_obj_count + 1;
    const size_t new_total_size = orig_obj_count + 2;

    // Build the incremental update text blocks
    // 1. Widget Annotation Object
    const size_t annot_offset = pdf_buffer.size();
    const std::string annot_str = std::format("{} 0 obj\n"
                                              "<< /Type /Annot\n"
                                              "   /Subtype /Widget\n"
                                              "   /FT /Sig\n"
                                              "   /T (Signature1)\n"
                                              "   /V {} 0 R\n"
                                              "   /Rect [ 0 0 0 0 ]\n"
                                              "   /F 132\n"
                                              "   /P 3 0 R\n"
                                              ">>\n"
                                              "endobj\n\n",
                                              annot_id, sig_id);
    pdf_buffer.insert(pdf_buffer.end(), annot_str.begin(), annot_str.end());

    // 2. Signature Dictionary Object Header
    const size_t sig_offset = pdf_buffer.size();
    const std::string sig_header = std::format("{} 0 obj\n"
                                               "<< /Type /Sig\n"
                                               "   /Filter /Adobe.PPKLite\n"
                                               "   /SubFilter /adbe.pkcs7.detached\n"
                                               "   /ByteRange [ ",
                                               sig_id);
    pdf_buffer.insert(pdf_buffer.end(), sig_header.begin(), sig_header.end());

    // 3. Four 10-digit placeholders for ByteRange: [ 0000000000 0000000000 0000000000 0000000000 ]
    const size_t byte_range_indices[4] = {
        pdf_buffer.size(),
        pdf_buffer.size() + 11,
        pdf_buffer.size() + 22,
        pdf_buffer.size() + 33,
    };
    const std::string_view byte_range_dummy = "0000000000 0000000000 0000000000 0000000000 ]\n   /Contents <";
    pdf_buffer.insert(pdf_buffer.end(), byte_range_dummy.begin(), byte_range_dummy.end());

    // Byte offset of the opening '<' delimiter:
    const size_t open_bracket_offset = pdf_buffer.size() - 1;
    // Byte offset where the hex signature characters begin:
    const size_t slot_offset = pdf_buffer.size();

    // 4. Exactly 8192 zeros for the CMS signature hex placeholder
    pdf_buffer.resize(pdf_buffer.size() + kDefaultSignatureSlotSize, '0');

    // 5. Signature Dictionary Closer
    const std::string_view sig_closer = ">\n   /Reason (Nordiska Document Verification)\n>>\nendobj\n\n";
    pdf_buffer.insert(pdf_buffer.end(), sig_closer.begin(), sig_closer.end());

    // Byte offset of byte immediately following '>':
    const size_t close_bracket_next = slot_offset + kDefaultSignatureSlotSize + 1;

    // 6. Cross-reference subsection for new objects
    const size_t new_xref_offset = pdf_buffer.size();
    const std::string xref_str =
        std::format("xref\n"
                    "{} 2\n"
                    "{:010d} 00000 n \n"
                    "{:010d} 00000 n \n"
                    "trailer\n"
                    "<< /Size {}\n"
                    "   /Root {}\n"
                    "   /Prev {}\n"
                    ">>\n"
                    "startxref\n"
                    "{}\n"
                    "%%EOF\n",
                    annot_id, annot_offset, sig_offset, new_total_size, root_ref, orig_xref, new_xref_offset);
    pdf_buffer.insert(pdf_buffer.end(), xref_str.begin(), xref_str.end());

    const size_t final_file_size = pdf_buffer.size();
    const size_t len1 = open_bracket_offset;
    const size_t offset2 = close_bracket_next;
    const size_t len2 = final_file_size - offset2;

    // 7. In-place patch the ByteRange numbers over the 10-digit placeholders
    write_10digit(pdf_buffer.data() + byte_range_indices[0], 0);
    write_10digit(pdf_buffer.data() + byte_range_indices[1], len1);
    write_10digit(pdf_buffer.data() + byte_range_indices[2], offset2);
    write_10digit(pdf_buffer.data() + byte_range_indices[3], len2);

    return SignatureSlot{
        .offset = slot_offset,
        .max_length = kDefaultSignatureSlotSize,
        .is_signed = false,
    };
}

} // namespace nordiska
