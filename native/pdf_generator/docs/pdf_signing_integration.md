# PDF signing integration (NOR-198, NOR-192, NOR-193)

## Ownership and flow

`PdfGenerator` owns the reusable rendering pipeline and a signer instance when
signing is enabled. Its default signer is a C++ RAII adapter over `pdf_sign.h`.
A custom `PdfSigner` remains available for tests and alternate integrations.
A generator/signer instance is intended for sequential reuse; do not share it
between concurrent signing calls without a signer thread-safety contract.

For each document:

1. Select the reserved `/Contents` capacity, measured in **hex characters**.
2. Render with spare output-buffer capacity for the incremental update.
3. Append a replacement catalog with an AcroForm signature field, a signature
   dictionary, and a new xref/trailer pointing back to the original revision.
4. Finalize `/ByteRange`, excluding the entire `<...>` signature value including
   its delimiters. SHA-256 hashes the two covered ranges without concatenation.
5. If enabled, call `pdf_signer_sign` exactly once with SHA-256, the 32-byte digest,
   and its length. The signer receives a digest, not PDF bytes or a private key.
6. Validate the returned hex length/content, copy into `/Contents`, and pad unused
   characters with ASCII `0`. No file bytes outside that slot change.
7. Compute the complete delivered artifact's SHA-256 and return the document.

The C result is disposed on success, error, and exceptions; the signer handle is
destroyed with its C++ owner. A failure aborts the whole customer batch. The C++
`GeneratorErrorKind` distinguishes preparation, hashing, invalid output and
capacity failures; adapter errors are translated at the generator boundary.
The existing C ABI reports `NORDISKA_PDF_SIGNING_FAILED` with a diagnostic.

`PdfDocument.signing_digest` is the hash of the signed byte ranges.
`PdfDocument.sha256_hash` is the hash of the complete delivered PDF, including the
signature and padding. `SignatureSlot.is_signed` means successful insertion,
**not** independent cryptographic verification, certificate trust, or qualified
signature status.

## Capacity policy

`GeneratorConfig.signature_contents_capacity` defaults to **8,192 hex
characters** (4,096 binary CMS bytes), matching the current signer's stub output.
The CLI exposes the same setting as `--signature-capacity`.

Capacity is passed as an argument for each render and preparation operation;
it is not encoded in each renderer or in the CMS-copy logic. A future
per-document selection can replace the configuration lookup without changing
those operations. No sizing negotiation or additional per-PDF signer call is
introduced in this branch.

The 105,032-character production sample in the reverse-engineering document is
an observed reservation, not a universal maximum or an exact CMS length.
Different signing configurations may require different reservations. The signer
and caller must agree on an upper bound before preparation/hashing. Oversized
results fail explicitly; we do not resize, rehash and silently sign again.

Haru reserves the tail when allocating its output buffer. Native and Cairo retain
the requested tail as their output buffers grow. The appender calculates the
actual final size and has a reserve fallback for direct callers; tests check that
current renderer outputs need no buffer reallocation during append or insertion.
The overhead allowance is an allocation hint, not a PDF format limit.

## Supported PDF structure

This is a writer for fresh PDFs from this component, not an arbitrary-PDF editor.
It accepts classic xref tables and generation-zero catalogs; encrypted PDFs,
previous incremental revisions, existing forms/version overrides, hybrid xrefs,
and xref streams are rejected. Cairo is restricted to PDF 1.4 serialization to
use classic tables, matching the other renderers. The appended catalog advertises
PDF 1.7 and preserves existing catalog entries; trailer `/Info` and `/ID` entries
are retained. The invisible signature field has no visual page annotation.

The signature dictionary uses `/ETSI.CAdES.detached`; the eventual signer must
return matching CAdES CMS. This label alone does not establish PAdES compliance
or any legal signature classification.

## C signer dependency and current limitation

The C implementation files were imported unchanged from
`feature/NOR-161-uppdatera-signerings-api`, commit
`eebfcb4d39bd6b88ad9b114aa6ada6875fb51ef0`. They remain in `native/pdf-signer`.
Only the signing implementation and its key/certificate/logging dependencies
were imported, not the branch's deployment/test provisioning files.

CMake builds a PIC static signer archive and links it into the generator targets,
using OpenSSL Crypto. No runtime `.so` lookup is needed. Set the CMake cache path
`NORDISKA_PDF_SIGNER_SOURCE_DIR` to consume a different compatible signer checkout.
The C API boundary is confined to `src/signing/pdf_signer.cpp`.

The current upstream `pdf_signer_create` already needs its configured PKCS#11
provider, SoftHSM token/key, PIN and matching certificate. Its configuration
includes `/usr/lib/libsofthsm2.so`, token `key-load-dev`, object `pdf-signer-test`,
`PDF_SIGNER_PKCS11_PIN`, and the working-directory-relative certificate
`tests/data/signing_cert.pem`. Generator startup does not provision these or
silently substitute a fake signer.

Even with that environment, `pdf_signer_sign` currently returns a repeating
8,192-character hex pattern, **not valid CMS**. PDFs containing it can exercise
placement but will not pass signature verification. This is an upstream stub
limitation, separate from the generator's tested signature construction.

## Usage and verification

Signing remains opt-in for the C++ configuration and CLI. The existing v1 C ABI
continues to return prepared, unsigned documents; its configuration remains
unchanged and its struct layout is not extended by this work.

```sh
cmake --build --preset default
ctest --test-dir build --output-on-failure
./build/pdf_generator --renderer native --signing --signature-capacity 8192 \
    --output build/signed-output docs/golden_customer_batch_sample.json
```

The last command requires the upstream signer environment described above.
Without `--signing`, preparation and hashing run without creating a C signer.

`nordiska_pdf_signing_tests` uses a C-boundary double to check the exact digest
request, one create/destroy per generator lifetime, one sign/dispose per PDF,
explicit-length output without a null terminator, and error cleanup. This double
is linked only into that test executable, never the production CLI/library.

Other tests cover multiple capacities, exact fit, oversized/malformed results,
unchanged covered bytes, artifact checksums, deferred mode, and all three engines.
Real detached CAdES signatures generated with a temporary self-signed test
certificate are embedded and decoded back out of each renderer's PDF. OpenSSL
verifies them and rejects a tampered covered byte; certificate-chain trust is
intentionally not required for this test.

CTest writes manual-review samples to `build/signing_samples/`:
`engine-0` is Haru, `engine-1` Cairo, and `engine-2` Native. `valid-test-cert` files
contain valid test signatures with an untrusted certificate; `stub` files contain
dummy bytes for structure/placement checks. They are disposable build outputs.

## Timing

`PipelineTiming.sign_seconds` measures the full signing phase. Its two nested
measurements are `hash_seconds` (signing digest and final-file checksum) and
`signer_call_seconds` (only `pdf_signer_sign`, excluding adapter copying and
result disposal). They are not added again to `total_seconds()`. No individual
buffer append/copy timers are collected. The benchmark's `--instrumented` output
reports these totals; external-call time is zero when signing is disabled.
