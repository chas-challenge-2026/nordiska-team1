#include "nordiska/signing/pdf_signer.hpp"

#include <arpa/inet.h>
#include <cerrno>
#include <chrono>
#include <cstdlib>
#include <cstring>
#include <memory>
#include <span>
#include <string>
#include <string_view>
#include <sys/socket.h>
#include <sys/time.h>
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
        return {SigningErrorKind::InvalidArgument, "Signer rejected request: invalid request format"};
    case SignerStatusUnsupportedVersion:
        return {SigningErrorKind::InternalError, "Signer rejected request: unsupported protocol version"};
    case SignerStatusUnsupportedAlgorithm:
        return {SigningErrorKind::InvalidArgument, "Signer rejected request: unsupported digest algorithm"};
    case SignerStatusSigningError:
        return {SigningErrorKind::SignatureGenerationFailed, "Signer failed to generate signature (crypto/key error)"};
    case SignerStatusInternalError:
        return {SigningErrorKind::InternalError, "Signer service reported internal error"};
    default:
        return {SigningErrorKind::InternalError, "Signer returned unknown protocol status: " + std::to_string(status)};
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

bool read_exact(int fd, uint8_t* buffer, size_t len) {
    size_t total_read = 0;
    while (total_read < len) {
        const ssize_t n = ::read(fd, buffer + total_read, len - total_read);
        if (n <= 0) {
            return false;
        }
        total_read += static_cast<size_t>(n);
    }
    return true;
}

bool write_exact(int fd, const uint8_t* buffer, size_t len) {
    size_t total_written = 0;
    while (total_written < len) {
        // A disconnected signer must return an I/O error without terminating the host process.
        const ssize_t n = ::send(fd, buffer + total_written, len - total_written, MSG_NOSIGNAL);
        if (n <= 0) {
            return false;
        }
        total_written += static_cast<size_t>(n);
    }
    return true;
}

class SocketPdfSigner final : public PdfSigner {
  public:
    explicit SocketPdfSigner(std::string socket_path, SignerMode sign_mode)
        : socket_path_(std::move(socket_path)), sign_mode_(sign_mode) {}

    std::expected<std::string, SigningError> sign_digest(std::span<const uint8_t, 32> digest, const SigningContext&,
                                                         double* call_seconds) override {
        using Clock = std::chrono::steady_clock;
        const auto start = call_seconds ? Clock::now() : Clock::time_point{};

        ScopedFd fd(::socket(AF_UNIX, SOCK_STREAM, 0));
        if (!fd.is_valid()) {
            return std::unexpected(
                SigningError{SigningErrorKind::InternalError,
                             "Failed to create UNIX domain socket: " + std::string(strerror(errno))});
        }

        // Set reasonable 10-second connect/read/write timeouts
        struct timeval tv {};
        tv.tv_sec = 10;
        tv.tv_usec = 0;
        ::setsockopt(fd.get(), SOL_SOCKET, SO_RCVTIMEO, &tv, sizeof(tv));
        ::setsockopt(fd.get(), SOL_SOCKET, SO_SNDTIMEO, &tv, sizeof(tv));

        struct sockaddr_un addr {};
        addr.sun_family = AF_UNIX;
        if (socket_path_.size() >= sizeof(addr.sun_path)) {
            return std::unexpected(
                SigningError{SigningErrorKind::InvalidArgument, "Signer socket path is too long: " + socket_path_});
        }
        std::memcpy(addr.sun_path, socket_path_.data(), socket_path_.size());
        addr.sun_path[socket_path_.size()] = '\0';

        if (::connect(fd.get(), reinterpret_cast<struct sockaddr*>(&addr), sizeof(addr)) != 0) {
            return std::unexpected(
                SigningError{SigningErrorKind::InternalError, "Failed to connect to PDF signer socket at '" +
                                                                  socket_path_ + "': " + std::string(strerror(errno))});
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

        if (!write_exact(fd.get(), request.data(), request.size())) {
            return std::unexpected(
                SigningError{SigningErrorKind::InternalError, "Failed to send signing request to PDF signer socket"});
        }

        // Read 8-byte response header
        std::array<uint8_t, kSignerResponseHeaderSize> response_header{};
        if (!read_exact(fd.get(), response_header.data(), response_header.size())) {
            return std::unexpected(
                SigningError{SigningErrorKind::InternalError, "Failed to read response header from PDF signer socket"});
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
            return std::unexpected(
                SigningError{SigningErrorKind::InvalidSignatureOutput, "Signer returned empty CMS hex output"});
        }

        if (hex_len > kSignerResponseMaxSize) {
            return std::unexpected(
                SigningError{SigningErrorKind::SignatureTooLarge, "Signer response size (" + std::to_string(hex_len) +
                                                                      ") exceeds maximum limit of " +
                                                                      std::to_string(kSignerResponseMaxSize)});
        }

        std::string hex_output;
        hex_output.resize(hex_len);
        if (!read_exact(fd.get(), reinterpret_cast<uint8_t*>(hex_output.data()), hex_len)) {
            return std::unexpected(SigningError{SigningErrorKind::InternalError,
                                                "Failed to read signature hex payload from PDF signer socket"});
        }

        if (call_seconds) {
            *call_seconds += std::chrono::duration<double>(Clock::now() - start).count();
        }

        return hex_output;
    }

  private:
    std::string socket_path_;
    SignerMode sign_mode_{SignerModePlain};
};

} // namespace

std::expected<std::unique_ptr<PdfSigner>, SigningError> create_native_pdf_signer() {
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

    return std::make_unique<SocketPdfSigner>(std::move(socket_path), mode);
}

} // namespace nordiska
