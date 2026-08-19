#include "nordiska/batch_create_pdf.hpp"

#include <filesystem>
#include <iostream>
#include <memory>
#include <stdexcept>
#include <span>
#include <string>
#include <utility>
#include <vector>

namespace {

class FakeRenderer final : public nordiska::IPdfRenderer {
  public:
    void render(const nordiska::Report& report, nordiska::IByteSink& sink) override {
        const std::string value = report.account_number + "\n" +
                                  std::to_string(report.transactions.size()) + "\n";
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
        std::filesystem::temp_directory_path() / "nordiska-pdf-application-tests";
    std::filesystem::remove_all(directory);
    std::filesystem::create_directories(directory);

    try {
        const auto output = directory / "single.txt";
        FakeRenderer renderer;
        nordiska::CreatePdf create_pdf(renderer);
        create_pdf.execute(valid_report("single"), output);
        require(std::filesystem::is_regular_file(output), "single output was not created");

        bool rejected = false;
        try {
            create_pdf.execute(nordiska::Report{}, directory / "invalid.txt");
        } catch (const std::invalid_argument&) {
            rejected = true;
        }
        require(rejected, "invalid report was accepted");

        nordiska::BatchCreatePdf batch([] { return std::make_unique<FakeRenderer>(); }, 2);
        std::vector<nordiska::BatchPdfRequest> requests;
        for (int index = 0; index < 4; ++index) {
            requests.push_back({valid_report("batch-" + std::to_string(index)),
                                directory / ("batch-" + std::to_string(index) + ".txt")});
        }
        batch.execute(requests);
        for (const auto& request : requests) {
            require(std::filesystem::is_regular_file(request.output_path),
                    "batch output was not created");
        }

        requests.push_back({nordiska::Report{}, directory / "rejected.txt"});
        rejected = false;
        try {
            batch.execute(requests);
        } catch (const std::runtime_error&) {
            rejected = true;
        }
        require(rejected, "batch failure was not reported");
    } catch (...) {
        std::filesystem::remove_all(directory);
        throw;
    }

    std::filesystem::remove_all(directory);
    std::cout << "application tests passed\n";
}
