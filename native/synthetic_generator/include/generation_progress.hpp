#pragma once

#include <cstdint>
#include <functional>
#include <string>

namespace nordiska::synthetic {

struct GenerationProgress {
    std::string stage = "Preparing database";
    double fraction = -1;
    int64_t customers = 0;
    int64_t transactions = 0;
    int64_t generated_customers = 0;
    int64_t transferred_customers = 0;
    double eta_seconds = -1;
    double customers_per_second = 0;
    int64_t completed = 0;
    int64_t total = 0;
    std::string unit = "customers";
};

using ProgressCallback = std::function<void(const GenerationProgress&)>;

} // namespace nordiska::synthetic

int generate_main(int argc, char** argv, const nordiska::synthetic::ProgressCallback& progress = {});
