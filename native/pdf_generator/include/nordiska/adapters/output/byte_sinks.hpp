#pragma once

#include "nordiska/ports/byte_sink.hpp"
#include "nordiska/ports/output_destination.hpp"

#include <filesystem>
#include <functional>
#include <memory>
#include <span>
#include <vector>

namespace nordiska {

class MemoryByteSink final : public IByteSink {
  public:
    void write(std::span<const std::byte> bytes) override;
    void finish() override;
    std::span<const std::byte> bytes() const noexcept;

  private:
    std::vector<std::byte> bytes_;
};

class NullByteSink final : public IByteSink {
  public:
    void write(std::span<const std::byte> bytes) override;
    void finish() override;
};
// JJ: this might be a good spot to hook in document signing via the callback function
class CallbackByteSink final : public IByteSink {
  public:
    using CompletionCallback = std::function<void(std::span<const std::byte>)>;

    explicit CallbackByteSink(CompletionCallback completion_callback);
    void write(std::span<const std::byte> bytes) override;
    void finish() override;

  private:
    MemoryByteSink buffer_;
    CompletionCallback completion_callback_;
    bool finished_{false};
};

class FileByteSink final : public IByteSink {
  public:
    explicit FileByteSink(std::filesystem::path output_path);
    ~FileByteSink() override;

    FileByteSink(const FileByteSink&) = delete;
    FileByteSink& operator=(const FileByteSink&) = delete;

    void write(std::span<const std::byte> bytes) override;
    void finish() override;

  private:
    std::filesystem::path output_path_;
    std::filesystem::path temporary_path_;
    class Impl;
    std::unique_ptr<Impl> impl_;
};

class FileOutputDestination final : public IOutputDestination {
  public:
    using PathFactory = std::function<std::filesystem::path(std::size_t)>;

    explicit FileOutputDestination(PathFactory path_factory);
    std::unique_ptr<IByteSink> open(DocumentMetadata metadata) override;

  private:
    PathFactory path_factory_;
};

class CallbackOutputDestination final : public IOutputDestination {
  public:
    using CompletionCallback = std::function<void(std::span<const std::byte>, std::size_t)>;

    explicit CallbackOutputDestination(CompletionCallback completion_callback);
    std::unique_ptr<IByteSink> open(DocumentMetadata metadata) override;

  private:
    CompletionCallback completion_callback_;
};

} // namespace nordiska
