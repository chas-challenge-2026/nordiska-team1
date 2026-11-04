# PDF Signer

Native C service for creating detached CMS/CAdES signatures for PDF documents.

The signer is designed as a separate process from the PDF generator. The PDF generator computes the SHA-256 digest of the PDF `ByteRange` and sends only that digest to the signer over a Unix domain socket. The signer loads the private key through PKCS#11, creates the CMS/CAdES signature, optionally adds an RFC 3161 timestamp token, and returns the final CMS value as uppercase hexadecimal data for the PDF `/Contents` field.

## Purpose

The main goals of the signer are to:

- keep private-key operations separated from PDF generation and application logic;
- support PKCS#11-backed private keys so the same signing flow can use SoftHSM during development and an HSM in production;
- avoid passing complete PDF documents into the signing service;
- support detached CMS/CAdES signatures suitable for PDF/PAdES signing;
- support RFC 3161 signature timestamps;
- keep key and certificate state long-lived instead of reloading it for every document;
- support concurrent signing requests through worker pools and bounded queues;
- provide a small and explicit binary protocol between the PDF generator and signer service.

The current implementation provides the cryptographic foundation for PAdES Baseline signatures, including timestamped signatures corresponding to the B-T level. Higher long-term validation levels such as B-LT and B-LTA require additional PDF-side validation data and document timestamp handling.

## Architecture

```text
PDF generator
    |
    | SHA-256 digest of signed PDF ByteRange
    v
Unix domain socket
    |
    v
PDF signer service
    |
    +--> PKCS#11 provider
    |      |
    |      +--> SoftHSM (development/test)
    |      \--> HSM (production)
    |
    +--> CMS/CAdES detached signature
    |
    \--> optional RFC 3161 timestamp
           |
           v
        TSA service
```

The signer returns the DER-encoded CMS object as uppercase hexadecimal data. The PDF generator is responsible for inserting that value into the PDF signature `/Contents` field.

## Key handling

Private keys are accessed through OpenSSL and PKCS#11.

Credentials are supplied on demand through callbacks rather than being stored as part of the key identity or loader configuration. Temporary credential buffers in the generic key-loader layer are explicitly cleared before release.

The signer verifies that the configured signing certificate matches the loaded private key before accepting signing requests.

## CMS/CAdES signing

The PDF generator supplies a 32-byte SHA-256 digest representing the signed PDF `ByteRange`.

The signer creates a detached CMS signature using:

- SHA-256;
- the configured X.509 signing certificate;
- the PKCS#11-backed private key;
- detached binary CMS;
- CAdES signing attributes.

Two signing modes are supported:

```text
SIGNER_MODE_PLAIN
SIGNER_MODE_TIMESTAMP
```

Timestamp mode additionally requests an RFC 3161 timestamp token and embeds it as the `id-aa-signatureTimeStampToken` unsigned CMS attribute.

## Signer protocol

The current protocol version is `2`.

A request is exactly 48 bytes:

```text
version          4 bytes
digest_algorithm 4 bytes
digest_len       4 bytes
sign_mode        4 bytes
SHA-256 digest   32 bytes
```

The response begins with an 8-byte header:

```text
status           4 bytes
contents_hex_len 4 bytes
```

When the status is `SIGNER_STATUS_OK`, the response header is followed by the uppercase hexadecimal CMS data.

## Concurrency

The signer service uses:

- 8 worker threads by default, customizable using environment variables;
- one long-lived signer instance per worker;
- a bounded client queue of 256 connections;
- one request per socket connection;
- socket send/receive timeouts.

The TSA HTTP server also uses a bounded queue and a configurable worker pool.

## Local TSA

A local RFC 3161 TSA is included for development, testing, and demonstrations.

The local TSA listens on `127.0.0.1:8081`, accepts `application/timestamp-query`, returns `application/timestamp-reply`, and uses a dedicated TSA certificate.

The local TSA is intended for development and demo use. Production deployments should use an appropriately trusted TSA and certificate chain.

## Dependencies

The native build requires:

- GCC
- GNU Make
- OpenSSL development libraries
- libcurl development libraries
- POSIX threads
- SoftHSM2 for development/test PKCS#11 storage
- an OpenSSL PKCS#11 provider
- OpenSC utilities

## Build

From `native/pdf-signer`:

```bash
make
```

To build only the runtime services:

```bash
make services
```

The service binaries are:

```text
pdf_signer_service
tsa_server_service
```

To clean generated binaries:

```bash
make clean
```

## Runtime configuration

### PKCS#11

```bash
PDF_SIGNER_PKCS11_PROVIDER=pkcs11
PDF_SIGNER_PKCS11_MODULE=/usr/lib/softhsm/libsofthsm2.so
PDF_SIGNER_PKCS11_URI='pkcs11:token=key-load-dev;object=pdf-signer-test;type=private'
PDF_SIGNER_PKCS11_PIN=<user-pin>
PDF_SIGNER_PKCS11_SO_PIN=<so-pin>
```

### Certificates

```bash
PDF_SIGNER_CERT_PATH=tests/data/signing_cert.pem
PDF_SIGNER_TSA_CERT_PATH=tests/data/tsa_cert.pem
```

### Signer socket

```bash
PDF_SIGNER_SOCKET_PATH=/tmp/pdf-signer.sock
```

### TSA

```bash
PDF_SIGNER_TSA_URL=http://127.0.0.1:8081/
TSA_WORKER_COUNT=8
TSA_HTTP_WORKER_COUNT=8
```

## Tests

Run the standard test suite with:

```bash
make test
```

Concurrency and stress tests are also available:

```bash
make test-signer-concurrency
make test-signer-stress
make test-tsa-stress
```

Docker variants:

```bash
make test-docker
make test-docker-debug
make test-signer-concurrency-docker
make test-signer-stress-docker
make test-tsa-stress-docker
```

## Docker

The development/demo container initializes SoftHSM, generates temporary signing and TSA certificates, imports the private key into SoftHSM, and starts both the TSA and signer services.

This startup key generation is intended for development and demo environments.

A production deployment should instead use provisioned certificates and a persistent or external PKCS#11/HSM-backed private key.

## Security notes

The signer is separated from the PDF generator so that the signing service receives only the digest that must be signed rather than the full PDF document.

The current implementation includes:

- PKCS#11-backed private-key access;
- on-demand credential callbacks;
- explicit clearing of temporary secret buffers;
- certificate/private-key consistency checks;
- bounded request queues;
- request validation;
- socket timeouts;
- loopback-only local TSA HTTP listener;
- graceful shutdown through `SIGINT` and `SIGTERM`.

## PAdES scope

The current signer supports detached CMS/CAdES signatures and RFC 3161 signature timestamps.

In practical PAdES terms:

- plain CMS/CAdES provides the basic detached PDF signature foundation;
- RFC 3161 signature timestamps provide the basis for PAdES B-T;
- B-LT additionally requires long-term validation data such as certificate chains and revocation information to be embedded in the PDF;
- B-LTA additionally requires document timestamps and archival renewal support.

Those higher-level PDF revisions are handled by the PDF layer and are not implemented by this signer service alone.
