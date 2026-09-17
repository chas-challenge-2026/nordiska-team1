#pragma once

#include "nordiska/domain/generated_pdfs.hpp"

#include <cstdint>
#include <vector>

namespace nordiska {

/**
 * Appends an ISO 32000-compliant digital signature placeholder block (incremental update)
 * to an existing visual PDF buffer in-place.
 *
 * Utilizes pre-allocated buffer capacity (guaranteed by the rendering backends) to avoid
 * heap reallocation and buffer copying.
 *
 * @param pdf_buffer The rendered visual PDF buffer. Must have spare capacity.
 * @return SignatureSlot containing the byte offset, slot length, and initial status.
 */
SignatureSlot append_signature_slot(std::vector<uint8_t>& pdf_buffer);

} // namespace nordiska
