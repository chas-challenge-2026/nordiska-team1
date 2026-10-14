#include "nordiska/application/pdf_generator.hpp"
#include "nordiska/rendering/pdf_engine.hpp"
#include "nordiska/signing/pdf_signer.hpp"
#include "nordiska/signing/signature_slot_appender.hpp"

#include <algorithm>
#include <arpa/inet.h>
#include <atomic>
#include <cerrno>
#include <cmath>
#include <cstdlib>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <openssl/cms.h>
#include <openssl/err.h>
#include <openssl/rsa.h>
#include <openssl/sha.h>
#include <openssl/x509.h>
#include <sstream>
#include <stdexcept>
#include <string>
#include <sys/socket.h>
#include <sys/un.h>
#include <thread>
#include <unistd.h>
#include <vector>

namespace {

void require(bool condition, const char* message) {
    if (!condition) {
        throw std::runtime_error(message);
    }
}

enum class MockSignerReply {
    Normal,
    Empty,
    Odd,
    NonHex,
    TooLarge,
    Failure,
    ProtocolVersionMismatch,
    UnsupportedAlgorithm
};

class MockUnixSignerServer {
  public:
    explicit MockUnixSignerServer(std::string socket_path) : socket_path_(std::move(socket_path)) {
        ::unlink(socket_path_.c_str());
        server_fd_ = ::socket(AF_UNIX, SOCK_STREAM, 0);
        require(server_fd_ >= 0, "mock server: socket failed");

        struct sockaddr_un addr {};
        addr.sun_family = AF_UNIX;
        std::strncpy(addr.sun_path, socket_path_.c_str(), sizeof(addr.sun_path) - 1);
        require(::bind(server_fd_, reinterpret_cast<struct sockaddr*>(&addr), sizeof(addr)) == 0,
                "mock server: bind failed");
        require(::listen(server_fd_, 16) == 0, "mock server: listen failed");

        running_ = true;
        server_thread_ = std::thread([this]() { run(); });
    }

    ~MockUnixSignerServer() {
        stop();
    }

    void stop() {
        if (running_) {
            running_ = false;
            // Connect to unblock accept if waiting
            int dummy_fd = ::socket(AF_UNIX, SOCK_STREAM, 0);
            if (dummy_fd >= 0) {
                struct sockaddr_un addr {};
                addr.sun_family = AF_UNIX;
                std::strncpy(addr.sun_path, socket_path_.c_str(), sizeof(addr.sun_path) - 1);
                ::connect(dummy_fd, reinterpret_cast<struct sockaddr*>(&addr), sizeof(addr));
                ::close(dummy_fd);
            }
            if (server_thread_.joinable()) {
                server_thread_.join();
            }
            if (server_fd_ >= 0) {
                ::close(server_fd_);
                server_fd_ = -1;
            }
            ::unlink(socket_path_.c_str());
        }
    }

    void set_reply(MockSignerReply reply) {
        reply_ = reply;
    }

    size_t request_count() const {
        return request_count_.load();
    }

    const std::vector<nordiska::Sha256Digest>& seen_digests() const {
        return seen_digests_;
    }

    bool oversized_reply_disconnected() const {
        return oversized_reply_disconnected_.load();
    }

    void clear_digests() {
        seen_digests_.clear();
    }

  private:
    void run() {
        while (running_) {
            int client_fd = ::accept(server_fd_, nullptr, nullptr);
            if (client_fd < 0) {
                if (!running_) {
                    break;
                }
                continue;
            }
            if (!running_) {
                ::close(client_fd);
                break;
            }
            handle_client(client_fd);
            ::close(client_fd);
        }
    }

    void handle_client(int fd) {
        uint8_t req[48];
        size_t total = 0;
        while (total < sizeof(req)) {
            ssize_t n = ::read(fd, req + total, sizeof(req) - total);
            if (n <= 0) {
                return;
            }
            total += static_cast<size_t>(n);
        }

        uint32_t version_net, algo_net, len_net, mode_net;
        std::memcpy(&version_net, req, 4);
        std::memcpy(&algo_net, req + 4, 4);
        std::memcpy(&len_net, req + 8, 4);
        std::memcpy(&mode_net, req + 12, 4);

        nordiska::Sha256Digest digest;
        std::memcpy(digest.data(), req + 16, 32);
        seen_digests_.push_back(digest);
        request_count_++;

        MockSignerReply rep = reply_.load();
        uint32_t status = 0;
        std::string hex;

        if (rep == MockSignerReply::Normal) {
            status = 0;
            for (size_t i = 0; i < 8192; ++i) {
                hex += "0123456789ABCDEF"[i % 16];
            }
        } else if (rep == MockSignerReply::Empty) {
            status = 0;
        } else if (rep == MockSignerReply::Odd) {
            status = 0;
            hex = "123";
        } else if (rep == MockSignerReply::NonHex) {
            status = 0;
            hex = "zz";
        } else if (rep == MockSignerReply::TooLarge) {
            status = 0;
            hex.assign(70000, 'A');
        } else if (rep == MockSignerReply::Failure) {
            status = 4; // SIGNER_STATUS_SIGNING_ERROR
        } else if (rep == MockSignerReply::ProtocolVersionMismatch) {
            status = 2; // SIGNER_STATUS_UNSUPPORTED_VERSION
        } else if (rep == MockSignerReply::UnsupportedAlgorithm) {
            status = 3; // SIGNER_STATUS_UNSUPPORTED_ALGORITHM
        }

        uint32_t status_net = htonl(status);
        uint32_t hex_len_net = htonl(static_cast<uint32_t>(hex.size()));
        uint8_t resp_hdr[8];
        std::memcpy(resp_hdr, &status_net, 4);
        std::memcpy(resp_hdr + 4, &hex_len_net, 4);
        const ssize_t w1 = ::send(fd, resp_hdr, sizeof(resp_hdr), MSG_NOSIGNAL);
        (void)w1;
        if (!hex.empty()) {
            if (rep == MockSignerReply::TooLarge) {
                // The client rejects the length header and closes without reading the body.
                // Wait for that close to exercise the broken-pipe path deterministically.
                char unused;
                (void)::recv(fd, &unused, sizeof(unused), 0);
            }
            const ssize_t w2 = ::send(fd, hex.data(), hex.size(), MSG_NOSIGNAL);
            if (rep == MockSignerReply::TooLarge) {
                oversized_reply_disconnected_ = (w2 < 0 && errno == EPIPE);
            }
        }
    }

    std::string socket_path_;
    int server_fd_{-1};
    std::atomic<bool> running_{false};
    std::atomic<MockSignerReply> reply_{MockSignerReply::Normal};
    std::atomic<size_t> request_count_{0};
    std::atomic<bool> oversized_reply_disconnected_{false};
    std::vector<nordiska::Sha256Digest> seen_digests_;
    std::thread server_thread_;
};

std::string_view text(const std::vector<uint8_t>& pdf) {
    return {reinterpret_cast<const char*>(pdf.data()), pdf.size()};
}

// Read the actual serialized ByteRange rather than using the appender metadata.
std::vector<uint8_t> covered_bytes(const std::vector<uint8_t>& pdf) {
    const auto document = text(pdf);
    const auto marker = document.rfind("/ByteRange [");
    require(marker != std::string_view::npos, "missing ByteRange");
    std::istringstream input(std::string(document.substr(marker + 12)));
    size_t start{}, first{}, second{}, length{};
    require(static_cast<bool>(input >> start >> first >> second >> length), "invalid ByteRange numbers");
    require(start == 0 && first < second && second <= pdf.size() && length == pdf.size() - second,
            "ByteRange must cover whole file except Contents");
    require(pdf[first] == '<' && pdf[second - 1] == '>', "ByteRange must exclude both delimiters");
    std::vector<uint8_t> bytes(pdf.begin(), pdf.begin() + first);
    bytes.insert(bytes.end(), pdf.begin() + second, pdf.end());
    return bytes;
}

nordiska::Sha256Digest independent_hash(const std::vector<uint8_t>& bytes) {
    nordiska::Sha256Digest digest{};
    require(SHA256(bytes.data(), bytes.size(), digest.data()) != nullptr, "SHA256 test helper failed");
    return digest;
}

void write_pdf(const std::filesystem::path& path, const std::vector<uint8_t>& bytes) {
    std::ofstream out(path, std::ios::binary);
    out.write(reinterpret_cast<const char*>(bytes.data()), static_cast<std::streamsize>(bytes.size()));
    require(out.good(), "could not write signing sample");
}

void test_renderers() {
    const nordiska::DocumentLayout layout{.pages = {
                                              {.texts = {{.x = 40, .y = 50, .text = "Signing integration test"}}},
                                              {.texts = {{.x = 40, .y = 50, .text = "Second page"}}},
                                          }};
    for (auto engine :
         {nordiska::PdfEngineKind::Libharu, nordiska::PdfEngineKind::Cairo, nordiska::PdfEngineKind::Native}) {
        nordiska::PdfEngine renderer(engine);
        for (size_t capacity : {8192U, 131072U}) {
            auto rendered = renderer.render(layout, capacity + nordiska::kSignatureUpdateOverhead);
            require(rendered.has_value(), "renderer failed");
            auto& pdf = *rendered;
            const auto original = pdf;
            const auto* address = pdf.data();
            auto untimed_pdf = pdf;
            auto untimed_slot = nordiska::append_signature_slot(untimed_pdf, capacity);
            nordiska::SignaturePreparationTiming preparation;
            auto slot = nordiska::append_signature_slot(pdf, capacity, &preparation);
            require(untimed_slot.has_value() && untimed_pdf == pdf,
                    "instrumentation must not change signature preparation output");
            require(slot.has_value(), slot ? "" : slot.error().message.c_str());
            require(pdf.data() == address, "signature append must not reallocate renderer output");
            require(std::equal(original.begin(), original.end(), pdf.begin()),
                    "incremental update modified original bytes");
            require(slot->max_length == capacity && !slot->is_signed, "slot metadata mismatch");
            require(text(pdf).find("/AcroForm << /SigFlags 3 /Fields [") != std::string_view::npos,
                    "signature field must be connected to catalog");
            auto digest = nordiska::compute_byte_range_digest(pdf, *slot);
            require(digest.has_value() && *digest == independent_hash(covered_bytes(pdf)), "wrong signing digest");
            const auto prepared = pdf;
            const auto prepared_slot = *slot;
            for (const auto& bad :
                 {std::string{}, std::string("123"), std::string("GG"), std::string(capacity + 2, 'A')}) {
                auto result = nordiska::insert_signature(pdf, *slot, bad);
                require(!result && pdf == prepared && !slot->is_signed, "bad result must fail without mutation");
            }
            auto inserted = nordiska::insert_signature(pdf, *slot, "30abCD");
            require(inserted.has_value() && slot->is_signed, "signature insertion failed");
            require(pdf.size() == prepared.size() && pdf.data() == address, "insertion resized PDF");
            require(text(pdf).substr(slot->offset, 6) == "30abCD", "hex must be copied without re-encoding");
            require(std::all_of(pdf.begin() + slot->offset + 6, pdf.begin() + slot->offset + capacity,
                                [](uint8_t c) { return c == '0'; }),
                    "remaining slot must contain ASCII zeros");
            require(nordiska::compute_byte_range_digest(pdf, *slot).value() == *digest,
                    "insertion changed signing digest");
            require(nordiska::hash_final_document(pdf).value() != nordiska::hash_final_document(prepared).value(),
                    "artifact hash did not change");
            require(!nordiska::insert_signature(pdf, *slot, "3000"), "second insertion must fail");
            auto exact_pdf = prepared;
            auto exact_slot = prepared_slot;
            require(nordiska::insert_signature(exact_pdf, exact_slot, std::string(capacity, 'F')).has_value(),
                    "exact-fit CMS rejected");
            require(!nordiska::append_signature_slot(pdf, capacity), "existing revision must be rejected");
            if (capacity == 8192) {
                write_pdf(std::filesystem::path("signing_samples") /
                              ("engine-" + std::to_string(static_cast<int>(engine)) + "-stub.pdf"),
                          pdf);
            }
        }
    }
    std::vector<uint8_t> bad{'n', 'o', 't', 'p', 'd', 'f'};
    const auto original = bad;
    require(!nordiska::append_signature_slot(bad) && bad == original, "malformed PDF must fail without mutation");
    require(!nordiska::append_signature_slot(bad, 0), "zero slot accepted");
    require(!nordiska::append_signature_slot(bad, 3), "odd slot accepted");
    require(!nordiska::compute_byte_range_digest(bad, {.offset = 1, .max_length = SIZE_MAX}), "overflow slot accepted");
}

void test_socket_signer_adapter() {
    const std::string socket_path = "/tmp/test_pdf_signer_" + std::to_string(::getpid()) + ".sock";
    ::setenv("PDF_SIGNER_SOCKET_PATH", socket_path.c_str(), 1);

    MockUnixSignerServer server(socket_path);

    const std::string input = R"({"customer_id":42,"documents":[
      {"document_id":"first","kind":"annual_tax_report","document":{"account_number":"1","tax_year":"2025"}},
      {"document_id":"second","kind":"annual_tax_report","document":{"account_number":"2","tax_year":"2025"}}
    ]})";
    const std::span<const uint8_t> payload(reinterpret_cast<const uint8_t*>(input.data()), input.size());
    nordiska::GeneratorConfig config{.engine = nordiska::PdfEngineKind::Native, .enable_signing = true};

    {
        nordiska::PdfGenerator generator(config);
        nordiska::PipelineTiming timing;
        for (int batch = 0; batch < 2; ++batch) {
            const auto previous_hash_time = timing.hash_seconds;
            const auto previous_call_time = timing.signer_call_seconds;
            auto result = generator.generate(payload, &timing);
            require(timing.hash_seconds > previous_hash_time, "hash timing must accumulate across batches");
            require(timing.signer_call_seconds > previous_call_time,
                    "external call timing must accumulate across batches");
            require(std::abs(timing.hash_seconds - timing.digest_seconds - timing.checksum_seconds) < 1e-9,
                    "hash total must equal its two passes");
            require(timing.sign_seconds >= timing.prepare_seconds + timing.digest_seconds + timing.checksum_seconds +
                                               timing.signer_wrapper_seconds + timing.insert_seconds,
                    "disjoint signing passes must fit their parent");
            const auto& p = timing.preparation;
            require(timing.prepare_seconds >= p.locate_xref_seconds + p.trailer_seconds + p.xref_entries_seconds +
                                                  p.catalog_seconds + p.metadata_copy_seconds + p.format_seconds +
                                                  p.buffer_write_seconds,
                    "preparation passes must fit their parent");
            require(timing.signer_wrapper_seconds >= timing.signer_call_seconds,
                    "external signing call must fit its wrapper");
            require(timing.sign_seconds >= timing.hash_seconds + timing.signer_call_seconds,
                    "nested timings must fit within the end-to-end signing phase");
            require(result.has_value(), result ? "" : result.error().message().c_str());
            require(result->documents.size() == 2, "wrong batch size");
            for (size_t i = 0; i < 2; ++i) {
                const auto& document = result->documents[i];
                require(document.signature_slot.is_signed, "signed metadata not set");
                const auto computed = independent_hash(covered_bytes(document.pdf_bytes));
                require(document.signing_digest == computed, "stored signing digest mismatch");
                require(server.seen_digests()[static_cast<size_t>(batch * 2) + i] == computed,
                        "Signer service received wrong digest");
                require(document.sha256_hash == independent_hash(document.pdf_bytes),
                        "final artifact checksum mismatch");
            }
        }
        require(server.request_count() == 4, "wrong signer request count");
    }

    // Test error responses from signer service
    for (auto mode :
         {MockSignerReply::Empty, MockSignerReply::Odd, MockSignerReply::NonHex, MockSignerReply::TooLarge,
          MockSignerReply::Failure, MockSignerReply::ProtocolVersionMismatch, MockSignerReply::UnsupportedAlgorithm}) {
        server.set_reply(mode);
        nordiska::PdfGenerator generator(config);
        auto result = generator.generate(payload);
        const auto expected_kind =
            mode == MockSignerReply::TooLarge                  ? nordiska::SigningErrorKind::SignatureTooLarge
            : mode == MockSignerReply::Failure                 ? nordiska::SigningErrorKind::SignatureGenerationFailed
            : mode == MockSignerReply::UnsupportedAlgorithm    ? nordiska::SigningErrorKind::InvalidArgument
            : mode == MockSignerReply::ProtocolVersionMismatch ? nordiska::SigningErrorKind::InternalError
                                                               : nordiska::SigningErrorKind::InvalidSignatureOutput;
        require(!result, "signer failures must fail generation");
        require(std::holds_alternative<nordiska::SigningError>(result.error().details),
                "error details must be SigningError");
        require(std::get<nordiska::SigningError>(result.error().details).kind == expected_kind,
                "signer failures must map to expected SigningErrorKind");
    }

    require(server.oversized_reply_disconnected(), "oversized reply must exercise a disconnected peer");

    // Test socket connection failure when server is stopped
    server.stop();
    {
        nordiska::PdfGenerator generator(config);
        auto result = generator.generate(payload);
        require(!result, "stopped socket server must cause signing failure");
    }

    config.enable_signing = false;
    for (auto engine :
         {nordiska::PdfEngineKind::Libharu, nordiska::PdfEngineKind::Cairo, nordiska::PdfEngineKind::Native}) {
        config.engine = engine;
        nordiska::PdfGenerator unsigned_generator(config);
        nordiska::PipelineTiming timing;
        auto result = unsigned_generator.generate(payload, &timing);
        require(result.has_value(), "unsigned generation failed without signer service");
        require(timing.checksum_seconds > 0.0 && timing.hash_seconds == timing.checksum_seconds,
                "unsigned generation must only hash the final artifact");
        require(timing.prepare_seconds == 0.0 && timing.digest_seconds == 0.0 && timing.signer_call_seconds == 0.0 &&
                    timing.insert_seconds == 0.0,
                "unsigned generation must skip all signature preparation and signing");

        nordiska::JsonIngestor ingestor;
        auto job = ingestor.ingest(payload);
        require(job.has_value(), "unsigned fixture ingestion failed");
        nordiska::PdfEngine renderer(engine);
        for (size_t i = 0; i < result->documents.size(); ++i) {
            const auto& document = result->documents[i];
            auto layout = nordiska::LayoutBuilder::build(job->documents[i]);
            require(layout.has_value(), "unsigned fixture layout failed");
            auto rendered = renderer.render(*layout);
            require(rendered.has_value(), "unsigned fixture render failed");
            require(document.pdf_bytes == *rendered, "unsigned PDF must equal the unmodified renderer output");
            const std::string pdf(document.pdf_bytes.begin(), document.pdf_bytes.end());
            require(pdf.find("/ByteRange") == std::string::npos && pdf.find("/Type /Sig") == std::string::npos,
                    "unsigned PDF must not contain an empty signature revision");
            require(!document.signature_slot.is_signed && document.signature_slot.max_length == 0 &&
                        document.signing_digest == nordiska::Sha256Digest{},
                    "unsigned metadata must not describe a prepared signature");
            require(document.sha256_hash == independent_hash(document.pdf_bytes),
                    "unsigned artifact checksum mismatch");
        }
    }

    ::unsetenv("PDF_SIGNER_SOCKET_PATH");
}

// Make a real detached test signature independently of the C stub. Verification
// reads the serialized ByteRange and CMS back out of the finished PDF.
void test_real_cms() {
    const std::unique_ptr<EVP_PKEY, decltype(&EVP_PKEY_free)> key(EVP_RSA_gen(2048), EVP_PKEY_free);
    const std::unique_ptr<X509, decltype(&X509_free)> cert(X509_new(), X509_free);
    require(key && cert, "test key/certificate allocation failed");
    require(X509_set_version(cert.get(), 2) == 1, "certificate version failed");
    ASN1_INTEGER_set(X509_get_serialNumber(cert.get()), 1);
    X509_gmtime_adj(X509_getm_notBefore(cert.get()), 0);
    X509_gmtime_adj(X509_getm_notAfter(cert.get()), 3600);
    X509_set_pubkey(cert.get(), key.get());
    auto* name = X509_get_subject_name(cert.get());
    X509_NAME_add_entry_by_txt(name, "CN", MBSTRING_ASC, reinterpret_cast<const unsigned char*>("PDF test only"), -1,
                               -1, 0);
    X509_set_issuer_name(cert.get(), name);
    require(X509_sign(cert.get(), key.get(), EVP_sha256()) > 0, "test certificate signing failed");

    for (auto engine :
         {nordiska::PdfEngineKind::Libharu, nordiska::PdfEngineKind::Cairo, nordiska::PdfEngineKind::Native}) {
        nordiska::PdfEngine renderer(engine);
        auto rendered = renderer.render(
            {.pages = {{.texts = {{.x = 40, .y = 50, .text = "Valid test signature; untrusted certificate"}}}}},
            16384 + nordiska::kSignatureUpdateOverhead);
        require(rendered.has_value(), "test rendering failed");
        auto& pdf = *rendered;
        auto slot = nordiska::append_signature_slot(pdf, 16384);
        require(slot.has_value(), "test signature preparation failed");
        auto covered = covered_bytes(pdf);
        const std::unique_ptr<BIO, decltype(&BIO_free)> data(
            BIO_new_mem_buf(covered.data(), static_cast<int>(covered.size())), BIO_free);
        const std::unique_ptr<CMS_ContentInfo, decltype(&CMS_ContentInfo_free)> cms(
            CMS_sign(cert.get(), key.get(), nullptr, data.get(), CMS_BINARY | CMS_DETACHED | CMS_CADES),
            CMS_ContentInfo_free);
        require(cms != nullptr, "test CMS signing failed");
        const int encoded_length = i2d_CMS_ContentInfo(cms.get(), nullptr);
        require(encoded_length > 0, "CMS length failed");
        std::vector<unsigned char> der(static_cast<size_t>(encoded_length));
        auto* cursor = der.data();
        require(i2d_CMS_ContentInfo(cms.get(), &cursor) == encoded_length, "CMS encoding failed");
        std::string hex;
        for (auto byte : der) {
            hex += "0123456789ABCDEF"[byte >> 4];
            hex += "0123456789ABCDEF"[byte & 15];
        }
        require(nordiska::insert_signature(pdf, *slot, hex).has_value(), "real CMS insertion failed");
        const auto slot_hex = text(pdf).substr(slot->offset, slot->max_length);
        std::vector<unsigned char> extracted;
        for (size_t i = 0; i < slot_hex.size(); i += 2) {
            auto nibble = [](char c) { return c <= '9' ? c - '0' : c - 'A' + 10; };
            extracted.push_back(static_cast<unsigned char>((nibble(slot_hex[i]) << 4) | nibble(slot_hex[i + 1])));
        }
        const auto* encoded = extracted.data();
        const std::unique_ptr<CMS_ContentInfo, decltype(&CMS_ContentInfo_free)> parsed(
            d2i_CMS_ContentInfo(nullptr, &encoded, static_cast<long>(extracted.size())), CMS_ContentInfo_free);
        require(parsed != nullptr, "embedded CMS could not be decoded");
        auto verify = [&]() {
            auto bytes = covered_bytes(pdf);
            const std::unique_ptr<BIO, decltype(&BIO_free)> input(
                BIO_new_mem_buf(bytes.data(), static_cast<int>(bytes.size())), BIO_free);
            const std::unique_ptr<BIO, decltype(&BIO_free)> output(BIO_new(BIO_s_null()), BIO_free);
            return CMS_verify(parsed.get(), nullptr, nullptr, input.get(), output.get(),
                              CMS_BINARY | CMS_NO_SIGNER_CERT_VERIFY);
        };
        require(verify() == 1, "embedded signature failed cryptographic verification");
        write_pdf(std::filesystem::path("signing_samples") /
                      ("engine-" + std::to_string(static_cast<int>(engine)) + "-valid-test-cert.pdf"),
                  pdf);
        pdf[10] ^= 1;
        require(verify() != 1, "tampered PDF unexpectedly verified");
        ERR_clear_error();
    }
}

} // namespace

int main() {
    std::filesystem::create_directories("signing_samples");
    test_renderers();
    test_socket_signer_adapter();
    test_real_cms();
}
