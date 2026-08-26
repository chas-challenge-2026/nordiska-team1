#pragma once

#include "nordiska/pdf_renderer.hpp"

#include <filesystem>

namespace nordiska {

class CreatePdf {
  public:
    explicit CreatePdf(IPdfRenderer& renderer);
    void execute(const Report& report, IByteSink& sink) const;
    void execute(const Report& report, const std::filesystem::path& output_path) const;

  private:
    IPdfRenderer& renderer_;
};
} // namespace nordiska
