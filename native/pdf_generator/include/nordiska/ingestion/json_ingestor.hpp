#pragma once

#include "nordiska/domain/pdf_rendering_job.hpp"

#include <cstdint>
#include <expected>
#include <memory>
#include <span>
#include <string>

namespace nordiska {

enum class JsonIngestorKind { Nlohmann, Simdjson };

enum class IngestErrorKind { InvalidInput, SyntaxError, InternalError };

struct IngestError {
    IngestErrorKind kind{IngestErrorKind::InvalidInput};
    std::string path;
    std::string message;

    [[nodiscard]] std::string formatted_message() const {
        if (path.empty()) {
            return message;
        }
        if (message.empty()) {
            return path;
        }
        return path + ": " + message;
    }
};

class JsonIngestor {
  public:
    struct Impl;

    explicit JsonIngestor(JsonIngestorKind kind = JsonIngestorKind::Nlohmann);
    ~JsonIngestor();

    JsonIngestor(JsonIngestor&&) noexcept;
    JsonIngestor& operator=(JsonIngestor&&) noexcept;

    JsonIngestor(const JsonIngestor&) = delete;
    JsonIngestor& operator=(const JsonIngestor&) = delete;

    [[nodiscard]] std::expected<PdfRenderingJob, IngestError> ingest(std::span<const uint8_t> json_utf8) const;

  private:
    std::unique_ptr<Impl> impl_;
};

} // namespace nordiska
