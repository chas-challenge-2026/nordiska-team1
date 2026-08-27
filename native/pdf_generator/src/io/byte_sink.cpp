#include "nordiska/byte_sink.hpp"

#include <atomic>
#include <fstream>
#include <functional>
#include <stdexcept>
#include <string>
#include <thread>

namespace nordiska {

void MemoryByteSink::write(std::span<const std::byte> bytes) {
    bytes_.insert(bytes_.end(), bytes.begin(), bytes.end());
}

void MemoryByteSink::finish() {}

CallbackByteSink::CallbackByteSink(CompletionCallback completion_callback)
    : completion_callback_(std::move(completion_callback)) {
    if (!completion_callback_) {
        throw std::invalid_argument("completion_callback must not be empty");
    }
}

void CallbackByteSink::write(std::span<const std::byte> bytes) {
    if (finished_) {
        throw std::logic_error("callback byte sink is already finished");
    }
    buffer_.write(bytes);
}

void CallbackByteSink::finish() {
    if (finished_) {
        throw std::logic_error("callback byte sink is already finished");
    }
    buffer_.finish();
    finished_ = true;
    completion_callback_(buffer_.bytes());
}

class FileByteSink::Impl {
  public:
    std::ofstream output;
};

FileByteSink::FileByteSink(std::filesystem::path output_path)
    : output_path_(std::move(output_path)), impl_(new Impl) {
    if (output_path_.empty()) {
        delete impl_;
        impl_ = nullptr;
        throw std::invalid_argument("output_path must not be empty");
    }

    static std::atomic<unsigned long long> sequence{0};
    const auto suffix = std::to_string(std::hash<std::thread::id>{}(std::this_thread::get_id())) +
                        "." + std::to_string(sequence.fetch_add(1));
    temporary_path_ = output_path_.string() + ".tmp." + suffix;
    impl_->output.open(temporary_path_, std::ios::binary | std::ios::trunc);
    if (!impl_->output) {
        delete impl_;
        impl_ = nullptr;
        throw std::runtime_error("could not open output file: " + temporary_path_.string());
    }
}

FileByteSink::~FileByteSink() {
    if (impl_ != nullptr) {
        impl_->output.close();
        std::error_code ignored;
        std::filesystem::remove(temporary_path_, ignored);
        delete impl_;
    }
}

void FileByteSink::write(std::span<const std::byte> bytes) {
    if (impl_ == nullptr) {
        throw std::logic_error("file byte sink is already finished");
    }
    impl_->output.write(reinterpret_cast<const char*>(bytes.data()),
                        static_cast<std::streamsize>(bytes.size()));
    if (!impl_->output) {
        throw std::runtime_error("could not write output file: " + temporary_path_.string());
    }
}

void FileByteSink::finish() {
    if (impl_ == nullptr) {
        throw std::logic_error("file byte sink is already finished");
    }
    impl_->output.close();
    if (impl_->output.fail()) {
        impl_->output.clear();
        std::error_code ignored;
        std::filesystem::remove(temporary_path_, ignored);
        delete impl_;
        impl_ = nullptr;
        throw std::runtime_error("could not close output file: " + output_path_.string());
    }
    try {
        std::filesystem::rename(temporary_path_, output_path_);
    } catch (...) {
        std::error_code ignored;
        std::filesystem::remove(temporary_path_, ignored);
        delete impl_;
        impl_ = nullptr;
        throw;
    }
    delete impl_;
    impl_ = nullptr;
}

void NullByteSink::write(std::span<const std::byte>) {}

void NullByteSink::finish() {}

} // namespace nordiska
