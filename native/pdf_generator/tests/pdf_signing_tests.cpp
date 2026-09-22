#include "nordiska/application/pdf_generator.hpp"
#include "nordiska/rendering/pdf_engine.hpp"
#include "nordiska/signing/signature_slot_appender.hpp"

#include <algorithm>
#include <cstdlib>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <openssl/cms.h>
#include <openssl/err.h>
#include <openssl/rsa.h>
#include <openssl/sha.h>
#include <openssl/x509.h>
#include <pdf_sign.h>
#include <sstream>
#include <stdexcept>

namespace {

void require(bool condition, const char* message) {
    if (!condition) {
        throw std::runtime_error(message);
    }
}

enum class Reply { Normal, Empty, Odd, NonHex, TooLarge, Failure };
Reply reply = Reply::Normal;
bool fail_create = false;
int creations = 0;
int destructions = 0;
int sign_calls = 0;
int disposals = 0;
std::vector<nordiska::Sha256Digest> seen_digests;

} // namespace

// This executable supplies only the C boundary double. Production links the
// unmodified developer implementation from the separate static archive.
struct pdf_signer {
    int marker = 123;
};

extern "C" pdf_sign_status_t pdf_signer_create(pdf_signer_t** out) {
    ++creations;
    *out = nullptr;
    if (fail_create) {
        return PDF_SIGN_CRYPTO_ERROR;
    }
    *out = new pdf_signer;
    return PDF_SIGN_OK;
}

extern "C" void pdf_signer_destroy(pdf_signer_t* signer) {
    if (signer) {
        ++destructions;
        delete signer;
    }
}

extern "C" pdf_sign_status_t pdf_signer_sign(pdf_signer_t* signer, const pdf_sign_request_t* request,
                                             pdf_sign_result_t* result) {
    ++sign_calls;
    if (!signer || signer->marker != 123 || !request || !request->digest || request->digest_len != 32 ||
        request->digest_algorithm != PDF_SIGN_DIGEST_SHA256) {
        return PDF_SIGN_INVALID_ARGUMENT;
    }
    nordiska::Sha256Digest digest;
    std::copy_n(request->digest, digest.size(), digest.begin());
    seen_digests.push_back(digest);
    std::string value;
    switch (reply) {
    case Reply::Normal:
        for (size_t i = 0; i < 8192; ++i) {
            value += "0123456789ABCDEF"[i % 16];
        }
        break;
    case Reply::Empty:
        break;
    case Reply::Odd:
        value = "123";
        break;
    case Reply::NonHex:
        value = "zz";
        break;
    case Reply::TooLarge:
        value.assign(8194, 'A');
        break;
    case Reply::Failure:
        value = "3000";
        break;
    }
    result->contents_hex = nullptr;
    result->contents_hex_len = value.size();
    if (!value.empty()) {
        // Deliberately no terminator: adapter must honor the explicit length.
        result->contents_hex = static_cast<char*>(std::malloc(value.size()));
        if (!result->contents_hex) {
            return PDF_SIGN_INTERNAL_ERROR;
        }
        std::memcpy(result->contents_hex, value.data(), value.size());
    }
    return reply == Reply::Failure ? PDF_SIGN_CRYPTO_ERROR : PDF_SIGN_OK;
}

extern "C" void pdf_sign_result_dispose(pdf_sign_result_t* result) {
    ++disposals;
    std::free(result->contents_hex);
    *result = {};
}

namespace {

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
            auto slot = nordiska::append_signature_slot(pdf, capacity);
            require(slot.has_value(), slot ? "" : slot.error().message.c_str());
            require(pdf.data() == address, "signature append must not reallocate renderer output");
            require(std::equal(original.begin(), original.end(), pdf.begin()),
                    "incremental update modified original bytes");
            require(slot->max_length == capacity && !slot->is_signed, "slot metadata mismatch");
            require(text(pdf).find("/AcroForm << /SigFlags 3 /Fields [") != std::string_view::npos,
                    "signature field must be connected to catalog");
            auto digest = nordiska::calculate_signing_digest(pdf, *slot);
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
            require(nordiska::calculate_signing_digest(pdf, *slot).value() == *digest,
                    "insertion changed signing digest");
            require(nordiska::calculate_pdf_hash(pdf).value() != nordiska::calculate_pdf_hash(prepared).value(),
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
    require(!nordiska::calculate_signing_digest(bad, {.offset = 1, .max_length = SIZE_MAX}), "overflow slot accepted");
}

void test_c_adapter() {
    const std::string input = R"({"customer_id":42,"documents":[
      {"document_id":"first","kind":"annual_tax_report","document":{"account_number":"1","tax_year":"2025"}},
      {"document_id":"second","kind":"annual_tax_report","document":{"account_number":"2","tax_year":"2025"}}
    ]})";
    const std::span<const uint8_t> payload(reinterpret_cast<const uint8_t*>(input.data()), input.size());
    nordiska::GeneratorConfig config{.engine = nordiska::PdfEngineKind::Native, .enable_signing = true};
    const int created_before = creations;
    const int destroyed_before = destructions;
    const int calls_before = sign_calls;
    const int disposed_before = disposals;
    {
        nordiska::PdfGenerator generator(config);
        require(creations == created_before + 1, "signer must be created once at generator initialization");
        nordiska::PipelineTiming timing;
        for (int batch = 0; batch < 2; ++batch) {
            const auto previous_hash_time = timing.hash_seconds;
            const auto previous_call_time = timing.signer_call_seconds;
            auto result = generator.generate(payload, &timing);
            require(timing.hash_seconds > previous_hash_time, "hash timing must accumulate across batches");
            require(timing.signer_call_seconds > previous_call_time,
                    "external call timing must accumulate across batches");
            require(timing.sign_seconds >= timing.hash_seconds + timing.signer_call_seconds,
                    "nested timings must fit within the end-to-end signing phase");
            require(result.has_value(), result ? "" : result.error().message.c_str());
            require(result->documents.size() == 2, "wrong batch size");
            for (size_t i = 0; i < 2; ++i) {
                const auto& document = result->documents[i];
                require(document.signature_slot.is_signed, "signed metadata not set");
                const auto computed = independent_hash(covered_bytes(document.pdf_bytes));
                require(document.signing_digest == computed, "stored signing digest mismatch");
                require(seen_digests[static_cast<size_t>(calls_before + batch * 2) + i] == computed,
                        "C API received wrong digest");
                require(document.sha256_hash == independent_hash(document.pdf_bytes),
                        "final artifact checksum mismatch");
            }
        }
        require(creations == created_before + 1 && sign_calls == calls_before + 4, "wrong signer call count");
    }
    require(destructions == destroyed_before + 1 && disposals == disposed_before + 4, "C resource cleanup mismatch");
    for (auto mode : {Reply::Empty, Reply::Odd, Reply::NonHex, Reply::TooLarge, Reply::Failure}) {
        reply = mode;
        const int before = disposals;
        const int calls = sign_calls;
        nordiska::PdfGenerator generator(config);
        auto result = generator.generate(payload);
        const auto expected_kind = mode == Reply::TooLarge  ? nordiska::GeneratorErrorKind::SignatureTooLarge
                                   : mode == Reply::Failure ? nordiska::GeneratorErrorKind::SigningError
                                                            : nordiska::GeneratorErrorKind::InvalidSignatureOutput;
        require(!result && result.error().kind == expected_kind, "signer failures must map to generator error kinds");
        require(disposals == before + 1 && sign_calls == calls + 1, "failure cleanup or early abort incorrect");
    }
    reply = Reply::Normal;
    fail_create = true;
    {
        const int calls = sign_calls;
        nordiska::PdfGenerator generator(config);
        require(!generator.generate(payload) && sign_calls == calls, "creation failure must not call sign");
    }
    fail_create = false;
    config.enable_signing = false;
    const int before = creations;
    nordiska::PdfGenerator deferred(config);
    nordiska::PipelineTiming deferred_timing;
    auto result = deferred.generate(payload, &deferred_timing);
    require(deferred_timing.hash_seconds > 0.0 && deferred_timing.signer_call_seconds == 0.0,
            "deferred mode must time hashing without recording an external call");
    require(result.has_value() && creations == before, "deferred mode must not initialize signer");
    require(!result->documents[0].signature_slot.is_signed, "deferred document marked signed");
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
    test_c_adapter();
    test_real_cms();
}
