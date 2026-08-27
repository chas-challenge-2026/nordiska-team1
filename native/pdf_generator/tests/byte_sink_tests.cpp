#include "nordiska/byte_sink.hpp"

#include <filesystem>
#include <fstream>
#include <iostream>
#include <span>
#include <stdexcept>
#include <string>

namespace {

void require(bool condition, const char* message) {
    if (!condition) {
        throw std::runtime_error(message);
    }
}

std::span<const std::byte> as_bytes(const std::string& value) {
    return std::as_bytes(std::span(value.data(), value.size()));
}

} // namespace

int main() {
    const std::filesystem::path directory =
        std::filesystem::temp_directory_path() / "nordiska-byte-sink-tests";
    std::filesystem::remove_all(directory);
    std::filesystem::create_directories(directory);

    try {
        nordiska::MemoryByteSink memory;
        memory.write(as_bytes("hello "));
        memory.write(as_bytes("world"));
        memory.finish();
        const std::string value(reinterpret_cast<const char*>(memory.bytes().data()),
                                memory.bytes().size());
        require(value == "hello world", "memory sink did not retain bytes");

        const auto output = directory / "output.bin";
        {
            nordiska::FileByteSink file(output);
            file.write(as_bytes("pdf bytes"));
            file.finish();
        }
        std::ifstream input(output, std::ios::binary);
        const std::string persisted((std::istreambuf_iterator<char>(input)), {});
        require(persisted == "pdf bytes", "file sink did not persist bytes");

        std::string callback_output;
        nordiska::CallbackByteSink callback_sink(
            [&callback_output](std::span<const std::byte> bytes) {
                callback_output.assign(reinterpret_cast<const char*>(bytes.data()), bytes.size());
            });
        callback_sink.write(as_bytes("callback bytes"));
        callback_sink.finish();
        require(callback_output == "callback bytes",
                "callback sink did not publish completed bytes");

        const auto failed_output = directory / "failed.bin";
        {
            nordiska::FileByteSink file(failed_output);
            file.write(as_bytes("discarded"));
        }
        require(!std::filesystem::exists(failed_output), "unfinished file sink published output");

        nordiska::NullByteSink null_sink;
        null_sink.write(as_bytes("ignored"));
        null_sink.finish();
    } catch (...) {
        std::filesystem::remove_all(directory);
        throw;
    }

    std::filesystem::remove_all(directory);
    std::cout << "byte sink tests passed\n";
}
