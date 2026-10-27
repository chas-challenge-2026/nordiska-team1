#pragma once

#include "nordiska/rendering/pdf_engine.hpp"

#include <cstddef>
#include <memory>

namespace nordiska {

std::unique_ptr<PdfEngine::Impl> create_haru_pdf_engine(bool compression);

namespace test {

struct HaruArenaStats {
    size_t total_capacity{0};
    size_t overflow_chunk_count{0};
    size_t base_chunk_capacity{0};
    bool budget_exceeded{false};
};

HaruArenaStats get_haru_arena_stats();
void reset_haru_arena();
void* haru_arena_allocate_for_test(size_t bytes);

} // namespace test

} // namespace nordiska
