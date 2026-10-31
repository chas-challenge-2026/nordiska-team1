#include "nordiska/signing/pdf_signer.hpp"

#include <algorithm>
#include <arpa/inet.h>
#include <cerrno>
#include <charconv>
#include <chrono>
#include <cstdlib>
#include <cstring>
#include <memory>
#include <poll.h>
#include <span>
#include <string>
#include <string_view>
#include <sys/socket.h>
#include <sys/un.h>
#include <unistd.h>
#include <utility>
#include <vector>

namespace nordiska {
namespace {

constexpr uint32_t kSignerProtocolVersion = 2;
constexpr uint32_t kSignerDigestSha256 = 1;
constexpr uint32_t kSignerSha256DigestLen = 32;
constexpr size_t kSignerRequestHeaderSize = 16;
constexpr size_t kSignerRequestSize = kSignerRequestHeaderSize + kSignerSha256DigestLen;
constexpr size_t kSignerResponseHeaderSize = 8;
constexpr size_t kSignerResponseMaxSize = 65536;

constexpr const char* kDefaultSocketPath = "/run/pdf-signer/pdf-signer.sock";

enum SignerProtocolStatus : uint32_t {
    SignerStatusOk = 0,
    SignerStatusInvalidRequest = 1,
    SignerStatusUnsupportedVersion = 2,
    SignerStatusUnsupportedAlgorithm = 3,
    SignerStatusSigningError = 4,
    SignerStatusInternalError = 5,
};

enum SignerMode : uint32_t {
    SignerModePlain = 0,
    SignerModeTimestamp = 1,
};

SigningError map_protocol_status(uint32_t status) {
    switch (status) {
    case SignerStatusInvalidRequest:
        return {SigningErrorKind::InvalidArgument, DiagnosticCode::SignerRejectedRequestInvalidRequestFormat};
    case SignerStatusUnsupportedVersion:
        return {SigningErrorKind::InternalError, DiagnosticCode::SignerRejectedRequestUnsupportedProtocolVersion};
    case SignerStatusUnsupportedAlgorithm:
        return {SigningErrorKind::InvalidArgument, DiagnosticCode::SignerRejectedRequestUnsupportedDigestAlgorithm};
    case SignerStatusSigningError:
        return {SigningErrorKind::SignatureGenerationFailed,
                DiagnosticCode::SignerFailedToGenerateSignatureCryptoKeyError};
    case SignerStatusInternalError:
        return {SigningErrorKind::InternalError, DiagnosticCode::SignerServiceReportedInternalError};
    default:
        return {SigningErrorKind::InternalError, DiagnosticCode::UnknownSignerStatus};
    }
}

class ScopedFd {
  public:
    explicit ScopedFd(int fd = -1) noexcept : fd_(fd) {}
    ~ScopedFd() noexcept {
        if (fd_ >= 0) {
            ::close(fd_);
        }
    }
    ScopedFd(const ScopedFd&) = delete;
    ScopedFd& operator=(const ScopedFd&) = delete;
    ScopedFd(ScopedFd&& other) noexcept : fd_(other.fd_) {
        other.fd_ = -1;
    }
    ScopedFd& operator=(ScopedFd&& other) noexcept {
        if (this != &other) {
            if (fd_ >= 0) {
                ::close(fd_);
            }
            fd_ = other.fd_;
            other.fd_ = -1;
        }
        return *this;
    }
    [[nodiscard]] int get() const noexcept {
        return fd_;
    }
    [[nodiscard]] bool is_valid() const noexcept {
        return fd_ >= 0;
    }

  private:
    int fd_{-1};
};

using Clock = std::chrono::steady_clock;
enum class SocketFailure { Io, Timeout };

class SigningDeadline {
  public:
    explicit SigningDeadline(Clock::time_point expires) : expires_(expires) {}
    [[nodiscard]] int remaining_ms() const noexcept {
        const auto now = Clock::now();
        if (now >= expires_) {
            return 0;
        }
        return static_cast<int>(std::chrono::ceil<std::chrono::milliseconds>(expires_ - now).count());
    }
    [[nodiscard]] std::expected<void, SocketFailure> wait(int fd, short events) const {
        for (;;) {
            const int remaining = remaining_ms();
            if (remaining == 0) {
                return std::unexpected(SocketFailure::Timeout);
            }
            pollfd descriptor{fd, events, 0};
            const int result = ::poll(&descriptor, 1, remaining);
            if (result < 0) {
                if (errno == EINTR) {
                    continue;
                }
                return std::unexpected(SocketFailure::Io);
            }
            if (result == 0) {
                continue;
            }
            if (descriptor.revents & POLLNVAL) {
                return std::unexpected(SocketFailure::Io);
            }
            // Let recv/send/getsockopt classify hangup/error, including buffered data.
            return {};
        }
    }

  private:
    Clock::time_point expires_;
};

struct CallTiming {
    double* seconds;
    Clock::time_point start;
    ~CallTiming() noexcept {
        if (seconds) {
            *seconds += std::chrono::duration<double>(Clock::now() - start).count();
        }
    }
};

std::expected<void, SocketFailure> connect_socket(int fd, const sockaddr_un& address, const SigningDeadline& deadline) {
    for (;;) {
        if (deadline.remaining_ms() == 0) {
            return std::unexpected(SocketFailure::Timeout);
        }
        if (::connect(fd, reinterpret_cast<const sockaddr*>(&address), sizeof(address)) == 0 || errno == EISCONN) {
            return {};
        }
        if (errno == EINTR) {
            continue;
        }
        if (errno == EAGAIN || errno == EWOULDBLOCK) {
            // AF_UNIX reports a full listen backlog as EAGAIN, without starting
            // an asynchronous connection. Retry within the same deadline.
            const int delay = std::min(5, deadline.remaining_ms());
            if (delay == 0) {
                return std::unexpected(SocketFailure::Timeout);
            }
            if (::poll(nullptr, 0, delay) < 0 && errno != EINTR) {
                return std::unexpected(SocketFailure::Io);
            }
            continue;
        }
        if (errno != EINPROGRESS && errno != EALREADY) {
            return std::unexpected(SocketFailure::Io);
        }
        if (auto ready = deadline.wait(fd, POLLOUT); !ready) {
            return ready;
        }
        int error{};
        socklen_t length = sizeof(error);
        if (::getsockopt(fd, SOL_SOCKET, SO_ERROR, &error, &length) != 0 || error != 0) {
            return std::unexpected(SocketFailure::Io);
        }
        return {};
    }
}

std::expected<void, SocketFailure> read_exact(int fd, std::span<uint8_t> buffer, const SigningDeadline& deadline) {
    while (!buffer.empty()) {
        if (deadline.remaining_ms() == 0) {
            return std::unexpected(SocketFailure::Timeout);
        }
        const ssize_t count = ::recv(fd, buffer.data(), buffer.size(), 0);
        if (count < 0) {
            if (errno == EINTR) {
                continue;
            }
            if (errno == EAGAIN || errno == EWOULDBLOCK) {
                if (auto ready = deadline.wait(fd, POLLIN); !ready) {
                    return ready;
                }
                continue;
            }
            return std::unexpected(SocketFailure::Io);
        }
        if (count == 0) {
            return std::unexpected(SocketFailure::Io);
        }
        buffer = buffer.subspan(static_cast<size_t>(count));
    }
    return {};
}

std::expected<void, SocketFailure> write_exact(int fd, std::span<const uint8_t> buffer,
                                               const SigningDeadline& deadline) {
    while (!buffer.empty()) {
        if (deadline.remaining_ms() == 0) {
            return std::unexpected(SocketFailure::Timeout);
        }
        const ssize_t count = ::send(fd, buffer.data(), buffer.size(), MSG_NOSIGNAL);
        if (count < 0) {
            if (errno == EINTR) {
                continue;
            }
            if (errno == EAGAIN || errno == EWOULDBLOCK) {
                if (auto ready = deadline.wait(fd, POLLOUT); !ready) {
                    return ready;
                }
                continue;
            }
            return std::unexpected(SocketFailure::Io);
        }
        if (count == 0) {
            return std::unexpected(SocketFailure::Io);
        }
        buffer = buffer.subspan(static_cast<size_t>(count));
    }
    return {};
}

SigningError socket_error(SocketFailure failure, DiagnosticCode io_code) noexcept {
    return failure == SocketFailure::Timeout ? SigningError{SigningErrorKind::Timeout, DiagnosticCode::SigningTimedOut}
                                             : SigningError{SigningErrorKind::InternalError, io_code};
}

class SocketPdfSigner final : public PdfSigner {
  public:
    explicit SocketPdfSigner(std::string socket_path, SignerMode sign_mode, std::chrono::milliseconds timeout)
        : socket_path_(std::move(socket_path)), sign_mode_(sign_mode), timeout_(timeout) {}

    std::expected<std::string, SigningError> sign_digest(std::span<const uint8_t, 32> digest,
                                                         double* call_seconds) override {
        const auto start = Clock::now();
        const CallTiming timing{call_seconds, start};
        const SigningDeadline deadline(start + timeout_);
        ScopedFd fd(::socket(AF_UNIX, SOCK_STREAM | SOCK_NONBLOCK | SOCK_CLOEXEC, 0));
        if (!fd.is_valid()) {
            return std::unexpected(SigningError{SigningErrorKind::InternalError, DiagnosticCode::SocketCreateFailed});
        }

        struct sockaddr_un addr {};
        addr.sun_family = AF_UNIX;
        if (socket_path_.size() >= sizeof(addr.sun_path)) {
            return std::unexpected(SigningError{SigningErrorKind::InvalidArgument, DiagnosticCode::SocketPathTooLong});
        }
        std::memcpy(addr.sun_path, socket_path_.data(), socket_path_.size());
        addr.sun_path[socket_path_.size()] = '\0';

        if (auto connected = connect_socket(fd.get(), addr, deadline); !connected) {
            return std::unexpected(socket_error(connected.error(), DiagnosticCode::SocketConnectFailed));
        }

        // Build 48-byte protocol v2 request
        std::array<uint8_t, kSignerRequestSize> request{};
        const uint32_t version_net = htonl(kSignerProtocolVersion);
        const uint32_t algo_net = htonl(kSignerDigestSha256);
        const uint32_t len_net = htonl(kSignerSha256DigestLen);
        const uint32_t mode_net = htonl(static_cast<uint32_t>(sign_mode_));

        std::memcpy(request.data(), &version_net, 4);
        std::memcpy(request.data() + 4, &algo_net, 4);
        std::memcpy(request.data() + 8, &len_net, 4);
        std::memcpy(request.data() + 12, &mode_net, 4);
        std::memcpy(request.data() + kSignerRequestHeaderSize, digest.data(), digest.size());

        if (auto sent = write_exact(fd.get(), request, deadline); !sent) {
            return std::unexpected(
                socket_error(sent.error(), DiagnosticCode::FailedToSendSigningRequestToPDFSignerSocket));
        }

        std::array<uint8_t, kSignerResponseHeaderSize> response_header{};
        if (auto received = read_exact(fd.get(), response_header, deadline); !received) {
            return std::unexpected(
                socket_error(received.error(), DiagnosticCode::FailedToReadResponseHeaderFromPDFSignerSocket));
        }

        uint32_t status_net = 0;
        uint32_t hex_len_net = 0;
        std::memcpy(&status_net, response_header.data(), 4);
        std::memcpy(&hex_len_net, response_header.data() + 4, 4);

        const uint32_t status = ntohl(status_net);
        const uint32_t hex_len = ntohl(hex_len_net);

        if (status != SignerStatusOk) {
            return std::unexpected(map_protocol_status(status));
        }

        if (hex_len == 0) {
            return std::unexpected(SigningError{SigningErrorKind::InvalidSignatureOutput,
                                                DiagnosticCode::SignerReturnedEmptyCMSHexOutput});
        }

        if (hex_len > kSignerResponseMaxSize) {
            return std::unexpected(
                SigningError{SigningErrorKind::SignatureTooLarge, DiagnosticCode::SignatureTooLarge});
        }

        std::string hex_output;
        hex_output.resize(hex_len);
        if (auto received = read_exact(fd.get(), {reinterpret_cast<uint8_t*>(hex_output.data()), hex_len}, deadline);
            !received) {
            return std::unexpected(
                socket_error(received.error(), DiagnosticCode::FailedToReadSignatureHexPayloadFromPDFSignerSocket));
        }
        if (deadline.remaining_ms() == 0) {
            return std::unexpected(socket_error(SocketFailure::Timeout, DiagnosticCode::UnexpectedFailure));
        }

        return hex_output;
    }

  private:
    std::string socket_path_;
    SignerMode sign_mode_{SignerModePlain};
    std::chrono::milliseconds timeout_;
};

} // namespace

std::expected<std::unique_ptr<PdfSigner>, SigningError> create_native_pdf_signer() {
    int timeout_ms = 10000;
    if (const char* value = std::getenv("NORDISKA_PDF_SIGNER_TIMEOUT_MS"); value && *value) {
        const std::string_view text(value);
        const auto [end, error] = std::from_chars(text.data(), text.data() + text.size(), timeout_ms);
        if (error != std::errc{} || end != text.data() + text.size() || timeout_ms <= 0) {
            return std::unexpected(SigningError{SigningErrorKind::InvalidArgument,
                                                DiagnosticCode::InvalidArgument,
                                                {.field = DiagnosticField::SignerTimeout}});
        }
    }
    std::string socket_path = kDefaultSocketPath;
    if (const char* env_path = std::getenv("PDF_SIGNER_SOCKET_PATH"); env_path != nullptr && env_path[0] != '\0') {
        socket_path = env_path;
    }

    SignerMode mode = SignerModePlain;
    if (const char* env_mode = std::getenv("PDF_SIGNER_MODE"); env_mode != nullptr) {
        if (std::strcmp(env_mode, "1") == 0 || strcasecmp(env_mode, "timestamp") == 0) {
            mode = SignerModeTimestamp;
        }
    }

    return std::make_unique<SocketPdfSigner>(std::move(socket_path), mode, std::chrono::milliseconds(timeout_ms));
}

} // namespace nordiska
