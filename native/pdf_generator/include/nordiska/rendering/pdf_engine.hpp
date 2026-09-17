#pragma once

#include "nordiska/layout/document_layout.hpp"

#include <cstdint>
#include <expected>
#include <memory>
#include <string>
#include <vector>

namespace nordiska {

enum class PdfEngineKind { Libharu, Cairo, Native };

struct PdfEngineConfig {
    PdfEngineKind kind{PdfEngineKind::Libharu};
    bool compression{true};
};

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
    struct Impl {
        virtual ~Impl() = default;
        [[nodiscard]] virtual std::expected<std::vector<uint8_t>, RenderError>
        render(const DocumentLayout& layout) const = 0;
    };

    explicit PdfEngine(PdfEngineConfig config = {});
    explicit PdfEngine(PdfEngineKind kind, bool compression = true)
        : PdfEngine(PdfEngineConfig{.kind = kind, .compression = compression}) {}
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
