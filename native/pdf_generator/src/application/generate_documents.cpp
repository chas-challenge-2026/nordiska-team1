#include "nordiska/application/generate_documents.hpp"

#include "nordiska/domain/report.hpp"

#include <algorithm>
#include <atomic>
#include <exception>
#include <mutex>
#include <stdexcept>
#include <string>
#include <thread>
#include <utility>
#include <vector>

namespace nordiska {

GenerateDocuments::GenerateDocuments(RendererFactory renderer_factory, std::size_t worker_count)
    : renderer_factory_(std::move(renderer_factory)), worker_count_(worker_count) {
    if (!renderer_factory_) {
        throw std::invalid_argument("renderer_factory must not be empty");
    }
    if (worker_count_ == 0) {
        worker_count_ = std::max<std::size_t>(1, std::thread::hardware_concurrency());
    }
}

std::vector<DocumentResult> GenerateDocuments::execute(std::span<const DocumentRequest> requests,
                                                       IOutputDestination& destination) const {
    std::vector<DocumentResult> results(requests.size());
    for (std::size_t index = 0; index < results.size(); ++index) {
        results[index].index = index;
    }
    if (requests.empty()) {
        return results;
    }

    const std::size_t worker_count = std::min(worker_count_, requests.size());
    std::atomic<std::size_t> next_index{0};
    std::mutex results_mutex;
    std::vector<std::jthread> workers;
    workers.reserve(worker_count);

    const auto fail = [&](std::size_t index, std::string message) {
        std::lock_guard lock(results_mutex);
        results[index].succeeded = false;
        results[index].failure = DocumentFailure::generation;
        results[index].error = std::move(message);
    };
    const auto fail_input = [&](std::size_t index, std::string message) {
        std::lock_guard lock(results_mutex);
        results[index].succeeded = false;
        results[index].failure = DocumentFailure::invalid_input;
        results[index].error = std::move(message);
    };
    const auto succeed = [&](std::size_t index) {
        std::lock_guard lock(results_mutex);
        results[index].succeeded = true;
    };

    for (std::size_t worker = 0; worker < worker_count; ++worker) {
        workers.emplace_back([&] {
            std::unique_ptr<IDocumentRenderer> renderer;
            std::string renderer_error;
            try {
                renderer = renderer_factory_();
                if (!renderer) {
                    renderer_error = "renderer factory returned null";
                }
            } catch (const std::exception& error) {
                renderer_error = std::string("renderer factory failed: ") + error.what();
            } catch (...) {
                renderer_error = "renderer factory failed: unknown error";
            }

            while (true) {
                const std::size_t index = next_index.fetch_add(1);
                if (index >= requests.size()) {
                    break;
                }
                if (!renderer_error.empty()) {
                    fail(index, renderer_error);
                    continue;
                }

                try {
                    validate_report(requests[index].report);
                    std::unique_ptr<IByteSink> sink = destination.open({index});
                    if (!sink) {
                        throw std::runtime_error("output destination returned null");
                    }
                    renderer->render(requests[index].report, *sink);
                    sink->finish();
                    succeed(index);
                } catch (const std::invalid_argument& error) {
                    fail_input(index, error.what());
                } catch (const std::exception& error) {
                    fail(index, error.what());
                } catch (...) {
                    fail(index, "unknown document generation error");
                }
            }
        });
    }

    return results;
}

} // namespace nordiska
