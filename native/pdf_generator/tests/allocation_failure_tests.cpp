#include "nordiska/application/pdf_generator.hpp"
#include "nordiska/c_api/pdf_generator_c_api.h"

#include <cstdlib>
#include <iostream>
#include <new>
#include <stdexcept>
#include <string_view>

namespace {
// GNU ld wrapping is confined to this executable. The production library has
// no fault-injection switch or allocator hook. Leave allocation disabled until
// the assertion/formatting has completed. Pipeline sweeps fail one allocation;
// the formatter is also checked separately under sustained allocation failure.
thread_local bool active{};
thread_local size_t remaining{};
thread_local size_t failures{};
thread_local bool sustained{};
void before_allocation() {
    if (!active) {
        return;
    }
    if (remaining != 0) {
        --remaining;
        return;
    }
    if (failures != 0 && !sustained) {
        return;
    }
    ++failures;
    throw std::bad_alloc{};
}
struct FailAllocations {
    explicit FailAllocations(size_t budget, bool keep_failing = false) {
        remaining = budget;
        failures = 0;
        sustained = keep_failing;
        active = true;
    }
    ~FailAllocations() {
        active = false;
    }
    FailAllocations(const FailAllocations&) = delete;
    FailAllocations& operator=(const FailAllocations&) = delete;
};
void require(bool condition, const char* message) {
    if (!condition) {
        throw std::runtime_error(message);
    }
}
constexpr std::string_view input =
    R"({"customer_id":918273645,"customer_name":"PRIVATE_CUSTOMER_918273645","documents":[{"document_id":"PRIVATE_CUSTOMER_918273645","kind":"account_statement","document":{"account_number":"PRIVATE_CUSTOMER_918273645","transactions":[{"date":"2026-01-01","description":"PRIVATE_CUSTOMER_918273645"}]}}]})";
auto bytes() {
    return std::span(reinterpret_cast<const uint8_t*>(input.data()), input.size());
}
int callback_count{};
size_t delivered_bytes{};
// Exercise allocation failures in host callback work too, without exceptions
// leaving the C ABI. Successful calls must still deliver their PDF normally.
template <typename Batch> int receive(const Batch* batch, void*) {
    ++callback_count;
    std::string copy(reinterpret_cast<const char*>(batch->documents[0].bytes), batch->documents[0].length);
    delivered_bytes = copy.size();
    return 0;
}
template <typename Call, typename Callback> void sweep_abi(Call call, Callback callback) {
    size_t failure_cases{};
    bool completed{};
    for (size_t budget = 0; budget < 4096; ++budget) {
        int status{};
        bool injected{};
        bool safe_diagnostic{};
        callback_count = 0;
        {
            FailAllocations fail(budget);
            status = call(bytes().data(), input.size(), callback, nullptr);
            injected = failures != 0;
            const std::string_view error = nordiska_pdf_v1_get_last_error();
            safe_diagnostic =
                injected ? error.ends_with("out of memory") && error.find("918273645") == error.npos : error.empty();
        }
        require(safe_diagnostic, "allocation failure produced an unsafe diagnostic");
        if (injected) {
            require(status == 6, "allocation failure must return OUT_OF_MEMORY through ABI");
            require(callback_count <= 1, "allocation failure repeated delivery");
            ++failure_cases;
        } else {
            require(status == 0 && callback_count == 1 && delivered_bytes > 0,
                    "ABI failed to recover after memory failure");
            completed = true;
            break;
        }
    }
    require(completed && failure_cases > 10, "allocation sweep missed pipeline work");
}
} // namespace

// Wrap real C++ allocation calls rather than merely throwing from a mock signer.
extern "C" void* __real__Znwm(size_t);
extern "C" void* __real__Znam(size_t);
extern "C" void* __wrap__Znwm(size_t size) {
    before_allocation();
    return __real__Znwm(size);
}
extern "C" void* __wrap__Znam(size_t size) {
    before_allocation();
    return __real__Znam(size);
}

int main() {
    ::setenv("NORDISKA_PDF_ENABLE_SIGNING", "0", 1);
    ::setenv("NORDISKA_PDF_ENGINE", "native", 1);
    // Warm process-lifetime resources before sweeping per-call allocations.
    require(nordiska_pdf_v1_generate_customer_batch(bytes().data(), input.size(), receive<nordiska_pdf_batch_view>,
                                                    nullptr) == 0,
            "warmup failed");
    for (auto kind : {nordiska::JsonIngestorKind::Nlohmann, nordiska::JsonIngestorKind::Simdjson}) {
        size_t failure_cases{};
        bool completed{};
        for (size_t budget = 0; budget < 4096; ++budget) {
            nordiska::JsonIngestor ingestor(kind);
            bool safe{};
            bool injected{};
            {
                FailAllocations fail(budget);
                const auto parsed = ingestor.ingest(bytes());
                injected = failures != 0;
                safe = injected ? !parsed && parsed.error().code == nordiska::DiagnosticCode::OutOfMemory
                                : parsed.has_value();
                if (injected) {
                    const auto text =
                        nordiska::format_diagnostic({parsed.error().code, nordiska::DiagnosticStage::Ingestion});
                    safe = safe && text.view() == "ingestion: out of memory";
                }
            }
            require(safe, "parser allocation failure escaped structured handling");
            if (!injected) {
                completed = true;
                break;
            }
            ++failure_cases;
            // nlohmann allocates in its noexcept DOM destructor. Faulting those
            // cleanup allocations terminates inside the dependency. Check its
            // initial allocation failure, and sweep simdjson (the ABI parser).
            if (kind == nordiska::JsonIngestorKind::Nlohmann) {
                completed = true;
                break;
            }
        }
        require(completed && failure_cases > 0, "parser allocation sweep did not inject failures");
    }
    {
        bool safe{};
        {
            FailAllocations fail(0, true);
            const auto text = nordiska::format_diagnostic({nordiska::DiagnosticCode::OutOfMemory});
            safe = text.view() == "boundary: out of memory" && failures == 0;
        }
        require(safe, "formatter allocated while reporting memory exhaustion");
    }
    // Render each backend directly to prove failures occur in rendering too.
    nordiska::JsonIngestor ingestor;
    const auto job = ingestor.ingest(bytes());
    const auto layout = nordiska::LayoutBuilder::build(job->documents.front(), {.customer_name = job->customer_name});
    for (auto kind :
         {nordiska::PdfEngineKind::Native, nordiska::PdfEngineKind::Libharu, nordiska::PdfEngineKind::Cairo}) {
        nordiska::PdfEngine engine(kind);
        size_t failure_cases{};
        bool completed{};
        for (size_t budget = 0; budget < 4096; ++budget) {
            bool injected{};
            bool succeeded{};
            {
                FailAllocations fail(budget);
                try {
                    succeeded = engine.render(*layout).has_value();
                } catch (const std::bad_alloc&) {
                }
                injected = failures != 0;
            }
            if (!injected) {
                require(succeeded, "renderer did not recover from allocation failure");
                completed = true;
                break;
            }
            ++failure_cases;
        }
        require(completed && failure_cases > 0, "renderer allocation sweep did not inject failures");
    }
    for (const char* engine : {"native", "haru", "cairo"}) {
        ::setenv("NORDISKA_PDF_ENGINE", engine, 1);
        sweep_abi(nordiska_pdf_v1_generate_customer_batch, receive<nordiska_pdf_batch_view>);
        sweep_abi(nordiska_pdf_v2_generate_customer_batch, receive<nordiska_pdf_batch_view_v2>);
        sweep_abi(nordiska_pdf_v1_generate_customer_batch_instrumented, receive<nordiska_pdf_batch_view>);
        sweep_abi(nordiska_pdf_v2_generate_customer_batch_instrumented, receive<nordiska_pdf_batch_view_v2>);
    }
    std::cout << "Allocation failure sweeps passed\n";
}
