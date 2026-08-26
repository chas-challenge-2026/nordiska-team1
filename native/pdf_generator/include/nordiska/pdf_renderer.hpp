#pragma once

#include "nordiska/byte_sink.hpp"
#include "nordiska/report.hpp"

namespace nordiska {

// This is the application's PDF boundary. No PDF-library types should cross it.
class IPdfRenderer {
  public:
    virtual ~IPdfRenderer() = default;
    virtual void render(const Report& report, IByteSink& sink) = 0;
};
} // namespace nordiska
