#include "nordiska/adapters/renderers/pdf/pdf_renderer.hpp"

#include "adapters/renderers/pdf/pdf_engine.hpp"

#include <iomanip>
#include <memory>
#include <sstream>
#include <stdexcept>

namespace nordiska {
namespace {

using pdf::detail::Document;
using pdf::detail::Page;
using pdf::detail::TextLine;
using pdf::detail::TextStyle;

std::string format_minor_units(std::int64_t amount_minor, const std::string& currency) {
    const bool negative = amount_minor < 0;
    const std::uint64_t absolute = negative ? static_cast<std::uint64_t>(-(amount_minor + 1)) + 1
                                            : static_cast<std::uint64_t>(amount_minor);
    std::ostringstream formatted;
    if (negative) {
        formatted << '-';
    }
    formatted << absolute / 100 << '.' << std::setw(2) << std::setfill('0') << absolute % 100 << ' '
              << currency;
    return formatted.str();
}

Document make_document(const Report& report) {
    constexpr std::size_t lines_per_page = 38;
    Document document;
    document.pages.emplace_back();

    const auto add_line = [&](std::string text, TextStyle style = TextStyle::body) {
        if (document.pages.back().lines.size() >= lines_per_page) {
            document.pages.emplace_back();
        }
        document.pages.back().lines.push_back({std::move(text), style});
    };

    add_line(report.title, TextStyle::title);
    add_line("Account: " + report.account_number);
    add_line("Transactions");
    for (const Transaction& transaction : report.transactions) {
        add_line(transaction.date + " | " + transaction.type + " | " +
                 format_minor_units(transaction.amount_minor, transaction.currency));
    }
    for (const std::string& line : report.summary_lines) {
        add_line(line);
    }
    return document;
}

} // namespace

class PdfRenderer::Impl {
  public:
    explicit Impl(std::unique_ptr<pdf::detail::IPdfEngine> engine) : engine_(std::move(engine)) {
        if (!engine_) {
            throw std::invalid_argument("PDF engine must not be null");
        }
    }

    void render(const Report& report, IByteSink& sink) {
        engine_->render(make_document(report), sink);
    }

  private:
    std::unique_ptr<pdf::detail::IPdfEngine> engine_;
};

PdfRenderer::PdfRenderer(std::unique_ptr<Impl> implementation)
    : implementation_(std::move(implementation)) {}

PdfRenderer::~PdfRenderer() = default;

void PdfRenderer::render(const Report& report, IByteSink& sink) {
    implementation_->render(report, sink);
}

std::unique_ptr<PdfRenderer> make_pdf_renderer(PdfEngine engine) {
    std::unique_ptr<pdf::detail::IPdfEngine> pdf_engine;
    if (engine == PdfEngine::haru) {
        pdf_engine = pdf::detail::make_haru_engine();
    } else {
        pdf_engine = pdf::detail::make_cairo_engine();
    }
    return std::unique_ptr<PdfRenderer>(
        new PdfRenderer(std::make_unique<PdfRenderer::Impl>(std::move(pdf_engine))));
}

} // namespace nordiska
