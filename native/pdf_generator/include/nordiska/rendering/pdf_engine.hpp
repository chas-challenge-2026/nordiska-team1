#pragma once

namespace nordiska {

enum class PdfEngineKind { Libharu, Cairo };

// Stub class: layout and PDF rendering engine seam.
// Backend engine implementation (Libharu / Cairo) is not yet implemented (scheduled for Step 3).
class PdfEngine {
  public:
    explicit PdfEngine(PdfEngineKind kind = PdfEngineKind::Libharu);
    ~PdfEngine();

    PdfEngine(PdfEngine&&) noexcept;
    PdfEngine& operator=(PdfEngine&&) noexcept;

    PdfEngine(const PdfEngine&) = delete;
    PdfEngine& operator=(const PdfEngine&) = delete;

  private:
    PdfEngineKind kind_{PdfEngineKind::Libharu};
};

} // namespace nordiska
