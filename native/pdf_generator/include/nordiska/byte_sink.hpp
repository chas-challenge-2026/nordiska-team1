#pragma once

#include <cstddef>
#include <filesystem>
#include <functional>
#include <span>
#include <vector>

namespace nordiska {

class IByteSink {
  public:
    virtual ~IByteSink() = default;
    virtual void write(std::span<const std::byte> bytes) = 0;
    virtual void finish() = 0;
};

class MemoryByteSink final : public IByteSink {
  public:
    void write(std::span<const std::byte> bytes) override;
    void finish() override;

    std::span<const std::byte> bytes() const noexcept {
        return bytes_;
    }

  private:
    std::vector<std::byte> bytes_;
};

// Buffers one completed document before handing it to a caller-owned output
// callback. The callback is invoked by finish() and is never retained after
// the sink is destroyed.
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
    Impl* impl_;
};

class NullByteSink final : public IByteSink {
  public:
    void write(std::span<const std::byte> bytes) override;
    void finish() override;
};

} // namespace nordiska
