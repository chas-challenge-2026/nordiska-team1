#include "nordiska/adapters/output/byte_sinks.hpp"
#include "nordiska/adapters/renderers/pdf/pdf_renderer.hpp"
#include "nordiska/application/generate_documents.hpp"

#include <cstring>
#include <filesystem>
#include <iostream>
#include <memory>
#include <span>
#include <stdexcept>
#include <string>
#include <utility>
#include <vector>

namespace {

class FakeRenderer final : public nordiska::IDocumentRenderer {
  public:
    void render(const nordiska::Report& report, nordiska::IByteSink& sink) override {
        const std::string value =
            report.account_number + "\n" + std::to_string(report.transactions.size()) + "\n";
        sink.write(std::as_bytes(std::span(value.data(), value.size())));
    }
};

nordiska::Report valid_report(std::string account_number) {
    return {std::move(account_number), {{"2026-01-05", "deposit", "SEK", 100000}}};
}

void require(bool condition, const char* message) {
    if (!condition) {
        throw std::runtime_error(message);
    }
}

} // namespace

int main() {
    const std::filesystem::path directory =
        std::filesystem::temp_directory_path() / "nordiska-document-application-tests";
    std::filesystem::remove_all(directory);
    std::filesystem::create_directories(directory);

    try {
        std::vector<nordiska::DocumentRequest> requests;
        requests.push_back({valid_report("single")});
        for (int index = 0; index < 4; ++index) {
            requests.push_back({valid_report("batch-" + std::to_string(index))});
        }

        nordiska::FileOutputDestination destination([&](std::size_t index) {
            return directory / ("document-" + std::to_string(index) + ".txt");
        });
        nordiska::GenerateDocuments generate([] { return std::make_unique<FakeRenderer>(); }, 2);
        const auto results = generate.execute(requests, destination);
        require(results.size() == requests.size(), "wrong result count");
        for (const auto& result : results) {
            require(result.succeeded, "valid document generation failed");
            require(std::filesystem::is_regular_file(
                        directory / ("document-" + std::to_string(result.index) + ".txt")),
                    "document output was not created");
        }

        auto cairo_renderer = nordiska::make_pdf_renderer(nordiska::PdfEngine::cairo);
        nordiska::MemoryByteSink cairo_output;
        cairo_renderer->render(valid_report("cairo"), cairo_output);
        cairo_output.finish();
        require(cairo_output.bytes().size() > 8, "Cairo output was empty");
        require(std::memcmp(cairo_output.bytes().data(), "%PDF-", 5) == 0,
                "Cairo output is not a PDF");

        requests.push_back({nordiska::Report{}});
        const auto failure_results = generate.execute(requests, destination);
        require(!failure_results.back().succeeded, "invalid report was accepted");
        require(failure_results.back().error.find("account_number") != std::string::npos,
                "invalid report error was not retained");
    } catch (...) {
        std::filesystem::remove_all(directory);
        throw;
    }

    std::filesystem::remove_all(directory);
    std::cout << "document application tests passed\n";
}
