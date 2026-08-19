#pragma once

#include <filesystem>

#include "nordiska/pdf_renderer.hpp"

namespace nordiska {

class CreatePdf {
public:
    explicit CreatePdf(IPdfRenderer& renderer);

    void execute(const Report& report,
                 const std::filesystem::path& output_path) const;

private:
    IPdfRenderer& renderer_;
};

} // namespace nordiska

