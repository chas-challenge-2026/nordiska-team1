#pragma once

#include "nordiska/pdf_renderer.hpp"

namespace nordiska {

// Temporary backend that writes a small valid PDF without a third-party
// dependency. It can later be replaced by LibHaruPdfRenderer or another
// adapter without changing CreatePdf or the report model.
class MinimalPdfRenderer final : public IPdfRenderer {
public:
    void render(const Report& report,
                const std::filesystem::path& output_path) override;
};

} // namespace nordiska

