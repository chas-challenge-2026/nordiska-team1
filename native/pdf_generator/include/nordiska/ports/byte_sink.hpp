#pragma once

#include <cstddef>
#include <span>
// JJ: uh If I have time it will be fun to try to do this as a concept instead
namespace nordiska {

class IByteSink {
  public:
    virtual ~IByteSink() = default;
    virtual void write(std::span<const std::byte> bytes) = 0;
    virtual void finish() = 0;
};

} // namespace nordiska
