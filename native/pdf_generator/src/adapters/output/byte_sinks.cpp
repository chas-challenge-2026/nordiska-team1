#include "nordiska/adapters/output/byte_sinks.hpp"

#include <atomic>
#include <fstream>
#include <functional>
#include <memory>
#include <stdexcept>
#include <string>
#include <thread>
#include <utility>

namespace nordiska {

void MemoryByteSink::write(std::span<const std::byte> bytes) {
    bytes_.insert(bytes_.end(), bytes.begin(), bytes.end());
}

void MemoryByteSink::finish() {}

std::span<const std::byte> MemoryByteSink::bytes() const noexcept {
    return bytes_;
}

void NullByteSink::write(std::span<const std::byte>) {}

void NullByteSink::finish() {}

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
    : output_path_(std::move(output_path)), impl_(std::make_unique<Impl>()) {
    if (output_path_.empty()) {
        throw std::invalid_argument("output_path must not be empty");
    }

    static std::atomic<unsigned long long> sequence{0};
    const auto suffix = std::to_string(std::hash<std::thread::id>{}(std::this_thread::get_id())) + "." +
                        std::to_string(sequence.fetch_add(1));
    temporary_path_ = output_path_.string() + ".tmp." + suffix;
    impl_->output.open(temporary_path_, std::ios::binary | std::ios::trunc);
    if (!impl_->output) {
        throw std::runtime_error("could not open output file: " + temporary_path_.string());
    }
}

FileByteSink::~FileByteSink() {
    if (impl_) {
        impl_->output.close();
        std::error_code ignored;
        std::filesystem::remove(temporary_path_, ignored);
    }
}

void FileByteSink::write(std::span<const std::byte> bytes) {
    if (!impl_) {
        throw std::logic_error("file byte sink is already finished");
    }
    impl_->output.write(reinterpret_cast<const char*>(bytes.data()), static_cast<std::streamsize>(bytes.size()));
    if (!impl_->output) {
        throw std::runtime_error("could not write output file: " + temporary_path_.string());
    }
}

void FileByteSink::finish() {
    if (!impl_) {
        throw std::logic_error("file byte sink is already finished");
    }
    impl_->output.close();
    if (impl_->output.fail()) {
        impl_->output.clear();
        std::error_code ignored;
        std::filesystem::remove(temporary_path_, ignored);
        impl_.reset();
        throw std::runtime_error("could not close output file: " + output_path_.string());
    }
    try {
        std::filesystem::rename(temporary_path_, output_path_);
    } catch (...) {
        std::error_code ignored;
        std::filesystem::remove(temporary_path_, ignored);
        impl_.reset();
        throw;
    }
    impl_.reset();
}

FileOutputDestination::FileOutputDestination(PathFactory path_factory) : path_factory_(std::move(path_factory)) {
    if (!path_factory_) {
        throw std::invalid_argument("path_factory must not be empty");
    }
}

std::unique_ptr<IByteSink> FileOutputDestination::open(DocumentMetadata metadata) {
    return std::make_unique<FileByteSink>(path_factory_(metadata.index));
}

CallbackOutputDestination::CallbackOutputDestination(CompletionCallback completion_callback)
    : completion_callback_(std::move(completion_callback)) {
    if (!completion_callback_) {
        throw std::invalid_argument("completion_callback must not be empty");
    }
}

std::unique_ptr<IByteSink> CallbackOutputDestination::open(DocumentMetadata metadata) {
    return std::make_unique<CallbackByteSink>([callback = completion_callback_, index = metadata.index](
                                                  std::span<const std::byte> bytes) { callback(bytes, index); });
}

} // namespace nordiska
