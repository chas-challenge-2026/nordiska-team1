#pragma once

#include "nordiska/layout/document_layout.hpp"

#include <cstdint>
#include <expected>
#include <memory>
#include <string>
#include <vector>

namespace nordiska {

enum class PdfEngineKind { Libharu, Cairo };

enum class RenderErrorKind {
    EngineError,
    OutOfMemory,
};

struct RenderError {
    RenderErrorKind kind{RenderErrorKind::EngineError};
    std::string message;
};

class PdfEngine {
  public:
    struct Impl;

    explicit PdfEngine(PdfEngineKind kind = PdfEngineKind::Libharu);
    ~PdfEngine();

    PdfEngine(PdfEngine&&) noexcept;
    PdfEngine& operator=(PdfEngine&&) noexcept;

    PdfEngine(const PdfEngine&) = delete;
    PdfEngine& operator=(const PdfEngine&) = delete;

    [[nodiscard]] std::expected<std::vector<uint8_t>, RenderError> render(const DocumentLayout& layout) const;

  private:
    std::unique_ptr<Impl> impl_;
};

} // namespace nordiska
