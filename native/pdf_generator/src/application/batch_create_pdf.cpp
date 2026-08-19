#include "nordiska/batch_create_pdf.hpp"

#include <algorithm>
#include <atomic>
#include <exception>
#include <mutex>
#include <sstream>
#include <stdexcept>
#include <thread>

namespace nordiska {

BatchCreatePdf::BatchCreatePdf(PdfRendererFactory renderer_factory, std::size_t worker_count)
    : renderer_factory_(std::move(renderer_factory)), worker_count_(worker_count) {
    if (!renderer_factory_) {
        throw std::invalid_argument("renderer_factory must not be empty");
    }
    if (worker_count_ == 0) {
        worker_count_ = std::max<std::size_t>(1, std::thread::hardware_concurrency());
    }
}

void BatchCreatePdf::execute(const std::vector<BatchPdfRequest>& requests) const {
    if (requests.empty()) {
        return;
    }

    const std::size_t worker_count = std::min(worker_count_, requests.size());
    std::atomic<std::size_t> next_index{0};
    std::mutex failures_mutex;
    std::vector<std::string> failures;
    std::vector<std::thread> workers;
    workers.reserve(worker_count);

    for (std::size_t worker = 0; worker < worker_count; ++worker) {
        workers.emplace_back([&] {
            std::unique_ptr<IPdfRenderer> renderer;
            try {
                renderer = renderer_factory_();
            } catch (const std::exception& error) {
                std::lock_guard lock(failures_mutex);
                failures.emplace_back(std::string("renderer factory failed: ") + error.what());
                return;
            } catch (...) {
                std::lock_guard lock(failures_mutex);
                failures.emplace_back("renderer factory failed: unknown error");
                return;
            }
            if (!renderer) {
                std::lock_guard lock(failures_mutex);
                failures.emplace_back("renderer factory returned null");
                return;
            }
            CreatePdf create_pdf(*renderer);
            while (true) {
                const std::size_t index = next_index.fetch_add(1);
                if (index >= requests.size()) {
                    break;
                }
                try {
                    create_pdf.execute(requests[index].report, requests[index].output_path);
                } catch (const std::exception& error) {
                    std::lock_guard lock(failures_mutex);
                    std::ostringstream message;
                    message << requests[index].output_path.string() << ": " << error.what();
                    failures.push_back(message.str());
                } catch (...) {
                    std::lock_guard lock(failures_mutex);
                    failures.push_back(requests[index].output_path.string() + ": unknown error");
                }
            }
        });
    }
    for (std::thread& worker : workers) {
        worker.join();
    }

    if (!failures.empty()) {
        std::sort(failures.begin(), failures.end());
        std::ostringstream message;
        message << failures.size() << " report(s) failed";
        for (const std::string& failure : failures) {
            message << "\n- " << failure;
        }
        throw std::runtime_error(message.str());
    }
}

} // namespace nordiska
