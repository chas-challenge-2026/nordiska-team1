#include "nordiska/diagnostics/diagnostic.hpp"

#include <algorithm>
#include <charconv>

namespace nordiska {
namespace {
std::string_view reason(DiagnosticCode code) noexcept {
    switch (code) {
    case DiagnosticCode::UnexpectedFailure:
        return "unexpected failure";
    case DiagnosticCode::OutOfMemory:
        return "out of memory";
    case DiagnosticCode::Required:
        return "required";
    case DiagnosticCode::ExpectedString:
        return "must be a string";
    case DiagnosticCode::ExpectedObject:
        return "must be an object";
    case DiagnosticCode::ExpectedArray:
        return "must be an array";
    case DiagnosticCode::ExpectedInteger:
        return "must be an integer";
    case DiagnosticCode::ExpectedUnsignedInteger:
        return "must be an unsigned integer";
    case DiagnosticCode::MustBePositive:
        return "must be greater than zero";
    case DiagnosticCode::MustNotBeEmpty:
        return "must not be empty";
    case DiagnosticCode::DuplicateDocumentId:
        return "duplicate document_id";
    case DiagnosticCode::UnsupportedDocumentKind:
        return "unsupported document kind";
    case DiagnosticCode::MalformedJson:
        return "malformed JSON";
    case DiagnosticCode::UnreadableField:
        return "field could not be read";
    case DiagnosticCode::InvalidArgument:
        return "invalid argument";
    case DiagnosticCode::PayloadTooLarge:
        return "payload exceeds maximum allowed limit";
    case DiagnosticCode::CallbackRejected:
        return "delivery callback rejected batch";
    case DiagnosticCode::InvalidEngine:
        return "unsupported renderer engine";
    case DiagnosticCode::InvalidIngestor:
        return "unsupported JSON ingestor";
    case DiagnosticCode::InputUnavailable:
        return "input could not be read";
    case DiagnosticCode::OutputUnavailable:
        return "output could not be written";
    case DiagnosticCode::InvalidOption:
        return "invalid command line option";
    case DiagnosticCode::MissingOptionValue:
        return "option requires a value";
    case DiagnosticCode::InvalidBoolean:
        return "invalid boolean option";
    case DiagnosticCode::InvalidSignatureCapacity:
        return "invalid signature capacity";
    case DiagnosticCode::InputRequired:
        return "input is required";
    case DiagnosticCode::UnsupportedDocumentType:
        return "unsupported document content type";
    case DiagnosticCode::CairoDrawingFailed:
        return "cairo drawing failed";
    case DiagnosticCode::CairoRenderingFailed:
        return "cairo rendering failed";
    case DiagnosticCode::UnknownSignerStatus:
        return "signer returned unknown protocol status";
    case DiagnosticCode::SocketCreateFailed:
        return "failed to create signer socket";
    case DiagnosticCode::SocketPathTooLong:
        return "signer socket path is too long";
    case DiagnosticCode::SocketConnectFailed:
        return "failed to connect to signer socket";
    case DiagnosticCode::SignatureTooLarge:
        return "signer response exceeds capacity";
    case DiagnosticCode::LibharuDrawingCommandFailed:
        return "libharu: drawing command failed";
    case DiagnosticCode::LibharuFailedToAddPage:
        return "libharu: failed to add page";
    case DiagnosticCode::LibharuFailedToCreateDocumentHandle:
        return "libharu: failed to create document handle";
    case DiagnosticCode::LibharuFailedToLoadRequiredHelveticaFonts:
        return "libharu: failed to load required Helvetica fonts";
    case DiagnosticCode::LibharuFailedToSerializePDFToStream:
        return "libharu: failed to serialize PDF to stream";
    case DiagnosticCode::LibharuMemoryArenaBudgetExceeded:
        return "libharu: memory arena budget exceeded";
    case DiagnosticCode::InvalidRGBImageResource:
        return "Invalid RGB image resource";
    case DiagnosticCode::NativeImageCompressionFailed:
        return "native: image compression failed";
    case DiagnosticCode::NativeEngineDocumentHasNoPages:
        return "native_engine: document has no pages";
    case DiagnosticCode::CairoFailedToCreatePDFSurface:
        return "cairo: failed to create PDF surface";
    case DiagnosticCode::CairoFailedToCreateDrawingContext:
        return "cairo: failed to create drawing context";
    case DiagnosticCode::CairoFailedToResolveAValidFontFaceTriedHelveticaArialSansSerif:
        return "cairo: failed to resolve a valid font face (tried 'Helvetica', 'Arial', 'sans-serif')";
    case DiagnosticCode::CMSHexExceedsReservedContentsCapacity:
        return "CMS hex exceeds reserved /Contents capacity";
    case DiagnosticCode::CatalogAlreadyContainsAFormOrVersionOverride:
        return "Catalog already contains a form or version override";
    case DiagnosticCode::CatalogMissingFromXref:
        return "Catalog missing from xref";
    case DiagnosticCode::CatalogXrefDoesNotPointToTheRootObject:
        return "Catalog xref does not point to the root object";
    case DiagnosticCode::ExistingRevisionsEncryptionAndHybridXrefsAreUnsupported:
        return "Existing revisions, encryption and hybrid xrefs are unsupported";
    case DiagnosticCode::InvalidCatalogDictionary:
        return "Invalid catalog dictionary";
    case DiagnosticCode::InvalidOrAlreadyFilledSignatureSlot:
        return "Invalid or already filled signature slot";
    case DiagnosticCode::InvalidSignatureSlotBounds:
        return "Invalid signature slot bounds";
    case DiagnosticCode::InvalidStartxrefOrEOF:
        return "Invalid startxref or EOF";
    case DiagnosticCode::InvalidTrailerDictionary:
        return "Invalid trailer dictionary";
    case DiagnosticCode::InvalidTrailerObjectReferences:
        return "Invalid trailer object references";
    case DiagnosticCode::InvalidXrefEntry:
        return "Invalid xref entry";
    case DiagnosticCode::InvalidXrefSubsection:
        return "Invalid xref subsection";
    case DiagnosticCode::MissingPDFHeader:
        return "Missing PDF header";
    case DiagnosticCode::MissingStartxref:
        return "Missing startxref";
    case DiagnosticCode::MissingTrailer:
        return "Missing trailer";
    case DiagnosticCode::OnlyClassicCrossReferenceTablesFromOurRenderersAreSupported:
        return "Only classic cross-reference tables from our renderers are supported";
    case DiagnosticCode::OpenSSLSHA256DigestFailed:
        return "OpenSSL SHA-256 digest failed";
    case DiagnosticCode::PDFExceeds10DigitXrefOffsets:
        return "PDF exceeds 10-digit xref offsets";
    case DiagnosticCode::PDFSignatureCapacityTooLarge:
        return "PDF signature capacity too large";
    case DiagnosticCode::RootIsNotAPageCatalog:
        return "Root is not a page catalog";
    case DiagnosticCode::SignatureCapacityMustBeAPositiveEvenNumberOfHexCharacters:
        return "Signature capacity must be a positive even number of hex characters";
    case DiagnosticCode::SignerReturnedInvalidCMSHex:
        return "Signer returned invalid CMS hex";
    case DiagnosticCode::TrailerMustContainSizeAndRoot:
        return "Trailer must contain /Size and /Root";
    case DiagnosticCode::UnexpectedBytesAfterPDFEOF:
        return "Unexpected bytes after PDF EOF";
    case DiagnosticCode::UnterminatedCatalogObject:
        return "Unterminated catalog object";
    case DiagnosticCode::FailedToReadResponseHeaderFromPDFSignerSocket:
        return "Failed to read response header from PDF signer socket";
    case DiagnosticCode::FailedToReadSignatureHexPayloadFromPDFSignerSocket:
        return "Failed to read signature hex payload from PDF signer socket";
    case DiagnosticCode::FailedToSendSigningRequestToPDFSignerSocket:
        return "Failed to send signing request to PDF signer socket";
    case DiagnosticCode::SignerFailedToGenerateSignatureCryptoKeyError:
        return "Signer failed to generate signature (crypto/key error)";
    case DiagnosticCode::SignerRejectedRequestInvalidRequestFormat:
        return "Signer rejected request: invalid request format";
    case DiagnosticCode::SignerRejectedRequestUnsupportedDigestAlgorithm:
        return "Signer rejected request: unsupported digest algorithm";
    case DiagnosticCode::SignerRejectedRequestUnsupportedProtocolVersion:
        return "Signer rejected request: unsupported protocol version";
    case DiagnosticCode::SignerReturnedEmptyCMSHexOutput:
        return "Signer returned empty CMS hex output";
    case DiagnosticCode::SignerServiceReportedInternalError:
        return "Signer service reported internal error";
    }
    return "unexpected failure";
}
std::string_view stage_name(DiagnosticStage stage) noexcept {
    switch (stage) {
    case DiagnosticStage::Boundary:
        return "boundary";
    case DiagnosticStage::Ingestion:
        return "ingestion";
    case DiagnosticStage::Layout:
        return "layout";
    case DiagnosticStage::Rendering:
        return "rendering";
    case DiagnosticStage::Signing:
        return "signing";
    case DiagnosticStage::Delivery:
        return "delivery";
    case DiagnosticStage::Cli:
        return "cli";
    }
    return "unknown";
}
} // namespace

std::string_view diagnostic_field_name(DiagnosticField field) noexcept {
    switch (field) {
    case DiagnosticField::None:
        return "";
    case DiagnosticField::Root:
        return "root";
    case DiagnosticField::CustomerId:
        return "customer_id";
    case DiagnosticField::Documents:
        return "documents";
    case DiagnosticField::DocumentId:
        return "document_id";
    case DiagnosticField::Kind:
        return "kind";
    case DiagnosticField::Document:
        return "document";
    case DiagnosticField::Transactions:
        return "transactions";
    case DiagnosticField::Date:
        return "date";
    case DiagnosticField::AmountMinor:
        return "amount_minor";
    case DiagnosticField::AccountNumber:
        return "account_number";
    case DiagnosticField::TaxYear:
        return "tax_year";
    case DiagnosticField::JsonUtf8:
        return "json_utf8";
    case DiagnosticField::JsonLength:
        return "json_length";
    case DiagnosticField::Callback:
        return "callback";
    case DiagnosticField::Engine:
        return "NORDISKA_PDF_ENGINE";
    case DiagnosticField::Input:
        return "input";
    case DiagnosticField::Output:
        return "output";
    case DiagnosticField::Compression:
        return "compression";
    case DiagnosticField::SignatureCapacity:
        return "signature_capacity";
    case DiagnosticField::Ingestor:
        return "ingestor";
    case DiagnosticField::Profile:
        return "profile";
    case DiagnosticField::Unknown:
        return "unknown";
    }
    return "unknown";
}

DiagnosticText format_diagnostic(Diagnostic diagnostic) noexcept {
    DiagnosticText result;
    const auto append = [&](std::string_view text) {
        const auto count = std::min(text.size(), result.bytes_.size() - 1 - result.length_);
        std::copy_n(text.data(), count, result.bytes_.data() + result.length_);
        result.length_ += count;
    };
    const auto index = [&](size_t value) {
        std::array<char, 32> digits{};
        const auto converted = std::to_chars(digits.data(), digits.data() + digits.size(), value);
        append({digits.data(), static_cast<size_t>(converted.ptr - digits.data())});
    };
    append(stage_name(diagnostic.stage));
    append(": ");
    const auto& location = diagnostic.location;
    if (location.document_index) {
        append("documents[");
        index(*location.document_index);
        append("]");
        if (location.scope == DiagnosticScope::Document || location.scope == DiagnosticScope::Transaction) {
            append(".document");
        }
        if (location.transaction_index) {
            append(".transactions[");
            index(*location.transaction_index);
            append("]");
        }
        if (location.field != DiagnosticField::None) {
            append(".");
        }
    }
    if (location.field != DiagnosticField::None) {
        append(diagnostic_field_name(location.field));
    }
    if (location.document_index || location.field != DiagnosticField::None) {
        append(": ");
    }
    append(reason(diagnostic.code));
    return result;
}
} // namespace nordiska
