#include "nordiska/application/pdf_generator.hpp"
#include "nordiska/c_api/pdf_generator_c_api.h"

#include <arpa/inet.h>
#include <array>
#include <chrono>
#include <csignal>
#include <cstdlib>
#include <cstring>
#include <iostream>
#include <pthread.h>
#include <stdexcept>
#include <string_view>
#include <sys/socket.h>
#include <sys/un.h>
#include <thread>
#include <unistd.h>

namespace {
using namespace std::chrono_literals;
using Clock = std::chrono::steady_clock;
void require(bool condition, const char* text) {
    if (!condition) {
        throw std::runtime_error(text);
    }
}
class Fd {
  public:
    explicit Fd(int value) : value_(value) {
        require(value >= 0, "test socket creation failed");
    }
    ~Fd() {
        ::close(value_);
    }
    Fd(const Fd&) = delete;
    Fd& operator=(const Fd&) = delete;
    int get() const {
        return value_;
    }

  private:
    int value_;
};
sockaddr_un address(const std::string& path) {
    sockaddr_un result{};
    result.sun_family = AF_UNIX;
    require(path.size() < sizeof(result.sun_path), "test socket path too long");
    std::memcpy(result.sun_path, path.c_str(), path.size() + 1);
    return result;
}
class Endpoint {
  public:
    std::string path;
    Fd listener{::socket(AF_UNIX, SOCK_STREAM, 0)};
    explicit Endpoint(int backlog = 4) : path("/tmp/pdf-deadline-" + std::to_string(::getpid()) + ".sock") {
        ::unlink(path.c_str());
        const auto addr = address(path);
        require(::bind(listener.get(), reinterpret_cast<const sockaddr*>(&addr), sizeof(addr)) == 0,
                "test bind failed");
        require(::listen(listener.get(), backlog) == 0, "test listen failed");
        ::setenv("PDF_SIGNER_SOCKET_PATH", path.c_str(), 1);
    }
    ~Endpoint() {
        ::unlink(path.c_str());
    }
};
enum class Reply { Success, Stall, TrickleHeader, TrickleBody, SplitStages, FirstThenStall };
void respond(int listener, Reply reply) {
    const int count = reply == Reply::FirstThenStall ? 2 : 1;
    for (int index = 0; index < count; ++index) {
        const int accepted = ::accept(listener, nullptr, nullptr);
        if (accepted < 0) {
            return;
        }
        Fd client(accepted);
        std::array<unsigned char, 48> request{};
        size_t read{};
        while (read < request.size()) {
            const auto size = ::recv(client.get(), request.data() + read, request.size() - read, 0);
            if (size <= 0) {
                return;
            }
            read += static_cast<size_t>(size);
        }
        const std::array<uint32_t, 2> header{htonl(0), htonl(4)};
        const std::string_view body = "3000";
        const auto send = [&](const void* data, size_t length) {
            return ::send(client.get(), data, length, MSG_NOSIGNAL) == static_cast<ssize_t>(length);
        };
        if (reply == Reply::Stall || (reply == Reply::FirstThenStall && index == 1)) {
            std::this_thread::sleep_for(220ms);
            return;
        }
        if (reply == Reply::TrickleHeader) {
            const auto* data = reinterpret_cast<const unsigned char*>(header.data());
            for (size_t i = 0; i < sizeof(header); ++i) {
                if (!send(data + i, 1)) {
                    return;
                }
                std::this_thread::sleep_for(40ms);
            }
        } else {
            if (reply == Reply::SplitStages) {
                std::this_thread::sleep_for(80ms);
            }
            if (!send(header.data(), sizeof(header))) {
                return;
            }
        }
        if (reply == Reply::TrickleBody) {
            for (const char& byte : body) {
                std::this_thread::sleep_for(40ms);
                if (!send(&byte, 1)) {
                    return;
                }
            }
        } else {
            if (reply == Reply::SplitStages) {
                std::this_thread::sleep_for(80ms);
            }
            if (!send(body.data(), body.size())) {
                return;
            }
        }
    }
}
constexpr std::string_view single =
    R"({"customer_id":1,"documents":[{"document_id":"PRIVATE_CUSTOMER","kind":"annual_tax_report","document":{"account_number":"PRIVATE_CUSTOMER","tax_year":"2026"}}]})";
constexpr std::string_view multiple =
    R"({"customer_id":1,"documents":[{"document_id":"first","kind":"annual_tax_report","document":{"account_number":"PRIVATE_CUSTOMER","tax_year":"2026"}},{"document_id":"second","kind":"annual_tax_report","document":{"account_number":"PRIVATE_CUSTOMER","tax_year":"2026"}}]})";
volatile std::sig_atomic_t signal_count{};
void interrupt_wait(int) {
    signal_count = signal_count + 1;
}
int callbacks{};
int receive_v1(const nordiska_pdf_batch_view*, void*) {
    ++callbacks;
    return 0;
}
int receive_v2(const nordiska_pdf_batch_view_v2*, void*) {
    ++callbacks;
    return 0;
}
template <typename Call, typename Callback> void exercise(Call call, Callback callback, bool instrumented) {
    for (auto reply : {Reply::Success, Reply::Stall, Reply::TrickleHeader, Reply::TrickleBody, Reply::SplitStages,
                       Reply::FirstThenStall}) {
        Endpoint endpoint;
        std::jthread server([&] { respond(endpoint.listener.get(), reply); });
        const auto input = reply == Reply::FirstThenStall ? multiple : single;
        callbacks = 0;
        const auto start = Clock::now();
        const int result = call(reinterpret_cast<const uint8_t*>(input.data()), input.size(), callback, nullptr);
        const auto elapsed = Clock::now() - start;
        if (reply == Reply::Success) {
            require(result == 0 && callbacks == 1, "fast response failed within budget");
            require(std::string_view(nordiska_pdf_v1_get_last_error()).empty(), "success retained an error");
        } else {
            require(result == 7 && callbacks == 0, "timeout delivered partial reports or changed ABI status");
            const std::string_view diagnostic = nordiska_pdf_v1_get_last_error();
            require(diagnostic.find("signing request timed out") != diagnostic.npos,
                    "missing controlled timeout reason");
            require(diagnostic.find("PRIVATE_CUSTOMER") == diagnostic.npos, "timeout leaked customer text");
            require(elapsed >= 60ms && elapsed < 750ms, "total signing deadline was not enforced");
            if (instrumented) {
                nordiska_pdf_pipeline_timing timing{};
                nordiska_pdf_v1_get_last_timing(&timing);
                require(timing.signer_call_seconds >= 0.06, "failed signing duration was not recorded");
            }
        }
    }
}
} // namespace
int main() {
    ::setenv("NORDISKA_PDF_ENABLE_SIGNING", "1", 1);
    ::setenv("NORDISKA_PDF_ENGINE", "native", 1);
    for (const char* value : {"0", "-1", "12ms", " 12", "2147483648", "PRIVATE_CUSTOMER"}) {
        ::setenv("NORDISKA_PDF_SIGNER_TIMEOUT_MS", value, 1);
        const auto signer = nordiska::create_native_pdf_signer();
        require(!signer && signer.error().kind == nordiska::SigningErrorKind::InvalidArgument,
                "invalid timeout configuration accepted");
        require(nordiska::format_diagnostic(
                    {signer.error().code, nordiska::DiagnosticStage::Signing, signer.error().location})
                        .view()
                        .find("PRIVATE_CUSTOMER") == std::string_view::npos,
                "invalid timeout value leaked");
    }
    for (const char* value : {"", "1", "2147483647"}) {
        ::setenv("NORDISKA_PDF_SIGNER_TIMEOUT_MS", value, 1);
        require(nordiska::create_native_pdf_signer().has_value(), "valid timeout configuration rejected");
    }
    ::unsetenv("NORDISKA_PDF_SIGNER_TIMEOUT_MS");
    require(nordiska::create_native_pdf_signer().has_value(), "default timeout configuration rejected");
    ::setenv("NORDISKA_PDF_SIGNER_TIMEOUT_MS", "120", 1);
    for (const char* mode : {"plain", "timestamp"}) {
        ::setenv("PDF_SIGNER_MODE", mode, 1);
        exercise(nordiska_pdf_v1_generate_customer_batch, receive_v1, false);
        exercise(nordiska_pdf_v2_generate_customer_batch, receive_v2, false);
        exercise(nordiska_pdf_v1_generate_customer_batch_instrumented, receive_v1, true);
        exercise(nordiska_pdf_v2_generate_customer_batch_instrumented, receive_v2, true);
    }
    // Full AF_UNIX listen backlog: connect itself must share the same budget.
    {
        Endpoint endpoint(0);
        Fd queued(::socket(AF_UNIX, SOCK_STREAM | SOCK_NONBLOCK, 0));
        const auto addr = address(endpoint.path);
        require(::connect(queued.get(), reinterpret_cast<const sockaddr*>(&addr), sizeof(addr)) == 0,
                "could not fill test backlog");
        auto signer = nordiska::create_native_pdf_signer();
        require(signer.has_value(), "could not construct deadline signer");
        nordiska::Sha256Digest digest{};
        double duration{};
        struct sigaction action {
        }, previous{};
        action.sa_handler = interrupt_wait;
        ::sigemptyset(&action.sa_mask);
        require(::sigaction(SIGUSR1, &action, &previous) == 0, "could not install test signal handler");
        const auto target = ::pthread_self();
        std::jthread interrupts([target](std::stop_token stop) {
            while (!stop.stop_requested()) {
                std::this_thread::sleep_for(10ms);
                ::pthread_kill(target, SIGUSR1);
            }
        });
        const auto start = Clock::now();
        const auto result = (*signer)->sign_digest(digest, &duration);
        interrupts.request_stop();
        interrupts.join();
        ::sigaction(SIGUSR1, &previous, nullptr);
        require(signal_count > 0, "test did not interrupt the connect wait");
        require(!result && result.error().kind == nordiska::SigningErrorKind::Timeout,
                "backlogged connect did not time out");
        require(Clock::now() - start < 750ms && duration >= 0.06, "connect exceeded budget or lost failure timing");
    }
    std::cout << "Signing deadline tests passed\n";
}
