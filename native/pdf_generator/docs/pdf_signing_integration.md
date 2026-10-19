# PDF signing integration (NOR-198, NOR-192, NOR-193)

## Ownership and flow

`PdfGenerator` owns the reusable rendering pipeline and a signer instance when
signing is enabled. Its default signer calls the external signer service over
a Unix domain socket using protocol v2.
A custom `PdfSigner` remains available for tests and alternate integrations.
A generator/signer instance is intended for sequential reuse; do not share it
between concurrent signing calls without a signer thread-safety contract.

For each document when signing is enabled:

1. Select the reserved `/Contents` capacity, measured in **hex characters**.
2. Render with spare output-buffer capacity for the incremental update.
3. Append a replacement catalog with an AcroForm signature field, a signature
   dictionary, and a new xref/trailer pointing back to the original revision.
4. Finalize `/ByteRange`, excluding the entire `<...>` signature value including
   its delimiters. SHA-256 hashes the two covered ranges without concatenation.
5. If enabled, send one protocol v2 request with SHA-256, the 32-byte digest,
   its length, and the signing mode. The signer receives a digest, not PDF bytes
   or a private key.
6. Validate the returned hex length/content, copy into `/Contents`, and pad unused
   characters with ASCII `0`. No file bytes outside that slot change.
7. Compute the complete delivered artifact's SHA-256 and return the document.

Each signing call owns its socket through RAII; it closes on success, error,
and exceptions. A failure aborts the whole customer batch. The C++
`GeneratorErrorPayload` variant preserves concrete subsystem errors (`SigningError`,
`IngestError`, `LayoutError`, `RenderError`, etc.) without information loss.
The existing C ABI reports `NORDISKA_PDF_SIGNING_FAILED` with a diagnostic.

`PdfDocument.signing_digest` is the hash of the signed byte ranges.
`PdfDocument.sha256_hash` is the hash of the complete delivered PDF, including the
signature and padding. `SignatureSlot.is_signed` means successful insertion,
**not** independent cryptographic verification, certificate trust, or qualified
signature status.

## Capacity policy

`GeneratorConfig.signature_contents_capacity` defaults to **8,192 hex
characters** (4,096 binary CMS bytes), matching the benchmark and mock signer output.
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

## External signer service

The generator does not compile or link `native/pdf-signer` sources. The private
adapter in `src/signing/pdf_signer.cpp` sends protocol v2 requests to a separately
running service. The service owns its keys, certificates and crypto configuration.

- `PDF_SIGNER_SOCKET_PATH`: socket path, default `/run/pdf-signer/pdf-signer.sock`.
- `PDF_SIGNER_MODE`: `1` or `timestamp` selects timestamp mode; otherwise plain mode.
- Requests contain four network-order 32-bit fields (version, algorithm, digest
  length, mode), followed by the 32-byte SHA-256 digest.
- Responses contain network-order status and hex-length fields, followed by CMS
  hex bytes. Empty responses and payloads over 65,536 characters fail explicitly.
  Insertion also validates hex encoding and the configured slot capacity.

OpenSSL Crypto remains a generator dependency for local SHA-256 hashing and the
signature tests. The previous in-process signer's `CMS_final_digest` requirement
and local OpenSSL auto-detection are no longer needed; system OpenSSL >= 3.0 is
sufficient. An explicit `OPENSSL_ROOT_DIR` override remains supported.

## Usage and verification

Signing is opt-in for the C++ configuration and CLI. When signing is disabled,
generation returns the renderer output without a signature revision or a
byte-range digest. It still computes the SHA-256 checksum of the final artifact.
The signature slot has zero capacity and the signing digest is zero. The
standalone signature preparation functions remain available for callers that
explicitly need deferred signing.

The C ABI selects signing through `NORDISKA_PDF_ENABLE_SIGNING`: `1` or `true`
enables it; any other set value disables it. When unset, signing is enabled if
the configured/default socket path is readable and writable. The public v1
struct layout is unchanged. Both v1 and v2 calls use this selection.

```sh
cmake --preset debug
cmake --build --preset debug
ctest --preset debug
PDF_SIGNER_SOCKET_PATH=/run/pdf-signer/pdf-signer.sock \
./build/debug/pdf_generator --renderer native --signing --signature-capacity 8192 \
    --output build/signed-output docs/golden_customer_batch_sample.json
```

The last command requires the external signer service. Without `--signing`, the
CLI returns unsigned renderer output and computes its checksum without
contacting the service.

`nordiska_pdf_signing_tests` uses a mock Unix socket server to check the digest
request, repeated batches, one request per PDF, malformed/oversized responses,
protocol errors, missing sockets, and unsigned mode. It does not require a running
signer service or provision keys.

Other tests cover multiple capacities, exact fit, oversized/malformed results,
unchanged covered bytes, artifact checksums, unsigned mode, and all three engines.
Real detached CAdES signatures generated with a temporary self-signed test
certificate are embedded and decoded back out of each renderer's PDF. OpenSSL
verifies them and rejects a tampered covered byte; certificate-chain trust is
intentionally not required for this test.

CTest writes manual-review samples to `signing_samples/` under its working
directory (`native/pdf_generator` for the configured suite):
`engine-0` is Haru, `engine-1` Cairo, and `engine-2` Native. `valid-test-cert` files
contain valid test signatures with an untrusted certificate; `stub` files contain
dummy bytes for structure/placement checks. They are disposable build outputs.

## Timing

`PipelineTiming.sign_seconds` measures the full signing phase. Preparation,
signing digest, signer wrapper, CMS insertion, and final checksum have separate
elapsed-time measurements. Preparation has nested inspection/format/write timers.
`hash_seconds` is the sum of digest and checksum time; `signer_call_seconds`
measures the successful socket call within the signer wrapper. Nested times are
not added again to `total_seconds()`. The benchmark's `--instrumented` output
reports these measurements; external-call time is zero for its fixed CMS stub.
