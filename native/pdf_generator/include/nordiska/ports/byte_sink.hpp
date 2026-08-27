#pragma once

#include <cstddef>
#include <span>

namespace nordiska {

class IByteSink {
  public:
    virtual ~IByteSink() = default;
    virtual void write(std::span<const std::byte> bytes) = 0;
    virtual void finish() = 0;
};

} // namespace nordiska
