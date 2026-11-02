#pragma once

#include <array>
#include <cstddef>
#include <optional>
#include <string_view>
#include <type_traits>

namespace nordiska {

// Closed vocabulary: diagnostics cannot own or borrow arbitrary text.
enum class DiagnosticCode {
    UnexpectedFailure,
    OutOfMemory,
    Required,
    ExpectedString,
    ExpectedObject,
    ExpectedArray,
    ExpectedInteger,
    ExpectedUnsignedInteger,
    MustBePositive,
    MustNotBeEmpty,
    DuplicateDocumentId,
    UnsupportedDocumentKind,
    MalformedJson,
    UnreadableField,
    InvalidArgument,
    PayloadTooLarge,
    CallbackRejected,
    InvalidEngine,
    InvalidIngestor,
    InputUnavailable,
    OutputUnavailable,
    InvalidOption,
    MissingOptionValue,
    InvalidBoolean,
    InvalidSignatureCapacity,
    InputRequired,
    UnsupportedDocumentType,
    CairoDrawingFailed,
    CairoRenderingFailed,
    UnknownSignerStatus,
    SocketCreateFailed,
    SocketPathTooLong,
    SocketConnectFailed,
    SignatureTooLarge,
    LibharuDrawingCommandFailed,
    LibharuFailedToAddPage,
    LibharuFailedToCreateDocumentHandle,
    LibharuFailedToLoadRequiredHelveticaFonts,
    LibharuFailedToSerializePDFToStream,
    LibharuMemoryArenaBudgetExceeded,
    InvalidRGBImageResource,
    NativeImageCompressionFailed,
    NativeEngineDocumentHasNoPages,
    CairoFailedToCreatePDFSurface,
    CairoFailedToCreateDrawingContext,
    CairoFailedToResolveAValidFontFaceTriedHelveticaArialSansSerif,
    CMSHexExceedsReservedContentsCapacity,
    CatalogAlreadyContainsAFormOrVersionOverride,
    CatalogMissingFromXref,
    CatalogXrefDoesNotPointToTheRootObject,
    ExistingRevisionsEncryptionAndHybridXrefsAreUnsupported,
    InvalidCatalogDictionary,
    InvalidOrAlreadyFilledSignatureSlot,
    InvalidSignatureSlotBounds,
    InvalidStartxrefOrEOF,
    InvalidTrailerDictionary,
    InvalidTrailerObjectReferences,
    InvalidXrefEntry,
    InvalidXrefSubsection,
    MissingPDFHeader,
    MissingStartxref,
    MissingTrailer,
    OnlyClassicCrossReferenceTablesFromOurRenderersAreSupported,
    OpenSSLSHA256DigestFailed,
    PDFExceeds10DigitXrefOffsets,
    PDFSignatureCapacityTooLarge,
    RootIsNotAPageCatalog,
    SignatureCapacityMustBeAPositiveEvenNumberOfHexCharacters,
    SignerReturnedInvalidCMSHex,
    TrailerMustContainSizeAndRoot,
    UnexpectedBytesAfterPDFEOF,
    UnterminatedCatalogObject,
    FailedToReadResponseHeaderFromPDFSignerSocket,
    FailedToReadSignatureHexPayloadFromPDFSignerSocket,
    FailedToSendSigningRequestToPDFSignerSocket,
    SignerFailedToGenerateSignatureCryptoKeyError,
    SignerRejectedRequestInvalidRequestFormat,
    SignerRejectedRequestUnsupportedDigestAlgorithm,
    SignerRejectedRequestUnsupportedProtocolVersion,
    SignerReturnedEmptyCMSHexOutput,
    SigningTimedOut,
    SignerServiceReportedInternalError,
};
enum class DiagnosticStage { Boundary, Ingestion, Layout, Rendering, Signing, Delivery, Cli };
enum class DiagnosticField {
    None,
    Root,
    CustomerId,
    Documents,
    DocumentId,
    Kind,
    Document,
    Transactions,
    Date,
    AmountMinor,
    AccountNumber,
    AccountType,
    InterestRateDisplay,
    TaxYear,
    JsonUtf8,
    JsonLength,
    Callback,
    Engine,
    Input,
    Output,
    Compression,
    SignatureCapacity,
    Ingestor,
    Profile,
    SignerTimeout,
    Unknown
};
enum class DiagnosticScope { Root, Envelope, Document, Transaction };

struct DiagnosticLocation {
    DiagnosticScope scope{DiagnosticScope::Root};
    DiagnosticField field{DiagnosticField::None};
    std::optional<size_t> document_index;
    std::optional<size_t> transaction_index;

    [[nodiscard]] constexpr DiagnosticLocation with_field(DiagnosticField value) const noexcept {
        auto result = *this;
        result.field = value;
        return result;
    }
    [[nodiscard]] constexpr DiagnosticLocation in_document() const noexcept {
        auto result = *this;
        result.scope = DiagnosticScope::Document;
        result.field = DiagnosticField::None;
        return result;
    }
    [[nodiscard]] constexpr DiagnosticLocation in_transaction(size_t index) const noexcept {
        auto result = in_document();
        result.scope = DiagnosticScope::Transaction;
        result.transaction_index = index;
        return result;
    }
};

struct Diagnostic {
    DiagnosticCode code{DiagnosticCode::UnexpectedFailure};
    DiagnosticStage stage{DiagnosticStage::Boundary};
    DiagnosticLocation location{};
};

// A bounded, allocation-free result also works while handling bad_alloc.
class DiagnosticText {
  public:
    [[nodiscard]] const char* c_str() const noexcept {
        return bytes_.data();
    }
    [[nodiscard]] std::string_view view() const noexcept {
        return {bytes_.data(), length_};
    }

  private:
    std::array<char, 512> bytes_{};
    size_t length_{};
    friend DiagnosticText format_diagnostic(Diagnostic) noexcept;
};

[[nodiscard]] std::string_view diagnostic_field_name(DiagnosticField field) noexcept;
[[nodiscard]] DiagnosticText format_diagnostic(Diagnostic diagnostic) noexcept;

static_assert(std::is_trivially_copyable_v<Diagnostic>);

} // namespace nordiska
