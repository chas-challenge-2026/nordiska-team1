# PDF Signer

Native C service for producing CMS signatures for PDF documents.

The service receives a SHA-256 digest over a Unix domain socket and returns an uppercase hexadecimal CMS signature suitable for writing directly into a PDF `/Contents` placeholder.

Supported modes:

- plain CMS/CAdES
- CMS/CAdES with an RFC 3161 timestamp token

The signer and the local TSA run as separate processes in the same container.

## Architecture

```text
PDF generator
    |
    | Unix domain socket
    v
pdf_signer_service
    |
    +-- PKCS#11 private key
    |
    +-- plain CMS
    |
    +-- timestamp mode --> tsa_server_service
                            |
                            +-- RFC 3161 timestamp response
```

Default runtime endpoints:

```text
Signer socket: /run/pdf-signer/pdf-signer.sock
TSA URL:       http://127.0.0.1:8081/
```

The TSA listens only on loopback and is not exposed outside the signer container.

## Requirements

The service image uses Ubuntu 26.04 and provides:

- OpenSSL 3.5+
- libcurl
- SoftHSM2
- pkcs11-provider
- OpenSC
- pthreads

SoftHSM is used for local development and tests.

## Build

From `native/pdf-signer`:

```bash
make services
```

This builds:

```text
pdf_signer_service
tsa_server_service
```

Build all test binaries:

```bash
make all
```

Clean build output:

```bash
make clean
```

## Tests

The recommended test path uses Docker because PKCS#11 tests require SoftHSM and the OpenSSL PKCS#11 provider.

From `native/pdf-signer`:

```bash
make test-docker
```

This initializes a SoftHSM token, creates test certificates, imports the private key, and runs the test suite.

Useful targets:

```bash
make test-key-load
make test-cert-load
make test-pdf-sign
make test-pkcs11
make test-tsa-client
make test-tsa-server
make test-signer-service
make test-tsa-stress
```

## Service image

Build from the repository root:

```bash
docker build \
  -f native/pdf-signer/docker/Dockerfile.service \
  -t nordiska-pdf-signer \
  .
```

The container starts both:

```text
tsa_server_service
pdf_signer_service
```

## Runtime configuration

Non-secret configuration is supplied through environment variables.

| Variable | Purpose |
| --- | --- |
| `PDF_SIGNER_PKCS11_PROVIDER` | OpenSSL PKCS#11 provider name |
| `PDF_SIGNER_PKCS11_MODULE` | PKCS#11 module path |
| `PDF_SIGNER_PKCS11_URI` | PKCS#11 private-key URI |
| `PDF_SIGNER_CERT_PATH` | Signing certificate path |
| `PDF_SIGNER_TSA_CERT_PATH` | TSA certificate path |
| `PDF_SIGNER_SOCKET_PATH` | Unix socket path |
| `PDF_SIGNER_TSA_URL` | Local TSA URL |
| `PDF_SIGNER_TSA_PORT` | TSA HTTP port |
| `PDF_SIGNER_TSA_TIMEOUT_MS` | TSA request timeout |

The runtime config layer owns environment parsing. Lower-level modules such as `key_load.c` remain independent of process configuration.

## Secrets

PKCS#11 credentials are supplied as Docker secrets.

Expected paths inside the container:

```text
/run/secrets/pdf_signer_pkcs11_pin
/run/secrets/pdf_signer_pkcs11_so_pin
```

The entrypoint exports only the user PIN as:

```text
PDF_SIGNER_PKCS11_PIN
```

The SO PIN is used only while initializing the development SoftHSM token and is not exported to the signer process.

Do not commit local secret files.

Example local setup from the repository root:

```bash
mkdir -p secrets

printf '1111\n' > secrets/pdf_signer_pkcs11_pin.txt
printf '0000\n' > secrets/pdf_signer_pkcs11_so_pin.txt
```

Ensure `secrets/` is ignored by Git.

## Compose integration

The signer runs as its own Compose service.

The application and signer share a named volume mounted at:

```text
/run/pdf-signer
```

The Unix socket is created at:

```text
/run/pdf-signer/pdf-signer.sock
```

No TCP port is required between the application and signer.

The TSA remains internal to the signer container on:

```text
127.0.0.1:8081
```

## Signer protocol

Protocol version:

```text
2
```

Request layout:

```text
offset  size  field
0       4     protocol version
4       4     digest algorithm
8       4     digest length
12      4     signing mode
16      32    SHA-256 digest
```

Constants:

```text
SIGNER_PROTOCOL_VERSION = 2
SIGNER_DIGEST_SHA256     = 1
SIGNER_MODE_PLAIN        = 0
SIGNER_MODE_TIMESTAMP    = 1
```

The request is 48 bytes.

Response header:

```text
offset  size  field
0       4     status
4       4     contents hex length
```

The response body contains the uppercase hexadecimal CMS value.

Maximum response size:

```text
65536 bytes
```

## PDF signing flow

The caller is responsible for PDF-specific work:

1. Build the PDF signature placeholder.
2. Calculate `/ByteRange`.
3. Compute SHA-256 over the ByteRange bytes.
4. Send the 32-byte digest and signing mode to `pdf_signer_service`.
5. Receive uppercase CMS hex.
6. Write the hex directly into `/Contents`.
7. Pad the unused placeholder with ASCII `0`.

The signer does not parse or modify PDF files.

## Timestamp flow

Timestamp mode:

1. Creates the detached CMS signature.
2. Extracts the raw CMS signature value.
3. Requests an RFC 3161 timestamp for its SHA-256 imprint.
4. Verifies the TSA response.
5. Adds the timestamp token as an unsigned CMS attribute.
6. Encodes the final CMS structure and returns uppercase hex.

## Ownership

`key_loader_t` owns its OpenSSL library context and providers.

Any `EVP_PKEY` loaded through a key loader must be disposed before the loader is destroyed.

Signer and TSA result objects own their output buffers and must be released with their matching dispose functions.

## Development setup

The development container provisions a SoftHSM token, private key, and certificates at startup.

This is for local development and integration testing. Production should use externally provisioned HSM-backed keys and certificates instead of generating them at container startup.
