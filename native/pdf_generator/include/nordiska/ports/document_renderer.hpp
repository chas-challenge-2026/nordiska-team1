#pragma once

#include "nordiska/domain/report.hpp"
#include "nordiska/ports/byte_sink.hpp"

namespace nordiska {

class IDocumentRenderer {
  public:
    virtual ~IDocumentRenderer() = default;
    virtual void render(const Report& report, IByteSink& sink) = 0;
};

} // namespace nordiska
