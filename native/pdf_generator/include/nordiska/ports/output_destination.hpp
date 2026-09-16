#pragma once

#include "nordiska/ports/byte_sink.hpp"

#include <cstddef>
#include <memory>

namespace nordiska {

struct DocumentMetadata {
    std::size_t index{};
};

class IOutputDestination {
  public:
    virtual ~IOutputDestination() = default;
    virtual std::unique_ptr<IByteSink> open(DocumentMetadata metadata) = 0;
};

} // namespace nordiska
