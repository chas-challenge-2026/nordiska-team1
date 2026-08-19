#pragma once

#include <atomic>
#include <filesystem>
#include <functional>
#include <stdexcept>
#include <string>
#include <thread>

namespace nordiska::detail {

template <typename Writer>
void write_atomically(const std::filesystem::path& output_path, Writer&& writer) {
    if (output_path.empty()) {
        throw std::invalid_argument("output_path must not be empty");
    }

    static std::atomic<unsigned long long> sequence{0};
    const auto suffix = std::to_string(std::hash<std::thread::id>{}(std::this_thread::get_id())) +
                        "." + std::to_string(sequence.fetch_add(1));
    const std::filesystem::path temporary_path = output_path.string() + ".tmp." + suffix;

    try {
        writer(temporary_path);
        std::filesystem::rename(temporary_path, output_path);
    } catch (...) {
        std::error_code ignored;
        std::filesystem::remove(temporary_path, ignored);
        throw;
    }
}

} // namespace nordiska::detail
