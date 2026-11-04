#pragma once

#include <cstddef>
#include <cstdint>
#include <span>

namespace nordiska::fuzz {

// Deliberately above the C API's 32 MiB admission limit. This harness calls
// the C++ pipeline directly, so it can exercise oversized JSON generation.
inline constexpr std::size_t kMaxInputBytes = 64 * 1024 * 1024;

// Engine-independent entry point. Returns false for normal pipeline rejection.
// Contract violations abort so fuzzing engines retain the triggering input.
[[nodiscard]] bool json_to_pdf(std::span<const std::uint8_t> input);

} // namespace nordiska::fuzz
