# Nordiska Native PDF Generator

C++23 module for generating customer PDF reports (account statements, annual summaries, and tax reports).
Designed for direct FFI / P-Invoke integration from .NET services and standalone native CLI execution on Linux.

---


## Architecture & Subsystems

```text
native/pdf_generator/
├── include/nordiska/             # Public C++ interface headers
│   ├── application/              # Orchestration (PdfGenerator, GeneratorConfig, PipelineTiming)
│   ├── c_api/                    # C89 ABI boundary (pdf_generator_c_api.h)
│   ├── domain/                   # CustomerBatch, Document, PdfRenderingJob
│   ├── ingestion/                # JSON ingestor interface & factory
│   ├── layout/                   # Deterministic layout structures (DocumentLayout)
│   ├── rendering/                # PDF rendering engine abstraction (PdfEngine)
│   ├── text/                     # Shared encoding and standard-font metrics
│   └── signing/                  # Digest signing interface & PDF preparation
├── src/                          # Subsystem implementations
│   ├── application/              # PdfGenerator pipeline driver
│   ├── c_api/                    # C ABI implementation & boundary validation
│   ├── ingestion/                # simdjson (default) & nlohmann JSON parsers
│   ├── layout/                   # LayoutBuilder (typography, tables, flow)
│   ├── rendering/                # Native, Haru (default, bump arena), Cairo
│   └── signing/                  # Socket signer adapter, PDF preparation & hashing
├── cli/                          # Standalone CLI binary (pdf_generator)
├── tests/                        # CTest automated test suites
├── tools/                        # Code formatters & OpenSSL setup helper
└── docs/                         # Golden customer batch specification & signing docs
```

### The Generation Pipeline
```
Raw JSON Buffer (UTF-8)
         │
         ▼  [1. Ingestion Stage]
   JsonIngestor (simdjson / nlohmann)
   - Validates envelope & versioning
   - Zero-copy string borrows where possible
         │
         ▼
   PdfRenderingJob (C++ domain model)
         │
         ▼  [2. Layout Stage]
   LayoutBuilder
   - Typography, column metrics & pagination
   - Composes branded page furniture and flowing tables
   - Generates ordered text, line, rectangle, and image commands
         │
         ▼
   DocumentLayout (deterministic coordinate geometry)
         │
         ▼  [3. Rendering Stage]
   PdfEngine (Native / Libharu / Cairo)
   - Compiles binary PDF 1.4 streams
         │
         ▼  [4. Optional Signing Stage]
   PdfSigner (Unix Domain Socket / Protocol v2)
   - Per-document signing dispatch to pdf-signer service
   - Strict all-or-nothing failure guarantee
         │
         ▼
GeneratedPdfs / C ABI Delivery Callback
```

See [Branded report layout](docs/report-design.md) for style/resource ownership,
IR commands, pagination, font substitution, and local synthetic previews.

---

## Pluggable Engines & Ingestors

### PDF Rendering Engines (`--renderer <engine>`)

1. **`native` (`PdfEngineKind::Native`)**:
   - Zero-dependency direct PDF 1.4 compiler.
   - Formats Core-14 PostScript Type 1 Helvetica and Helvetica-Bold font dictionaries, text matrices (`BT`, `Tf`, `Tm`, `Tj`, `ET`), colored vector paths, rectangles, and shared image XObjects.
   - Zero-allocation numeric formatting via `<charconv>` (`std::to_chars`).
   - Reuses precompressed image pixels directly. See the report-design notes for the measured cost of branded output.
2. **`haru` (`PdfEngineKind::Libharu`) — Default**:
   - Classical C library backend optimized with a thread-local bump arena (`HaruBumpArena` via `HPDF_NewEx`).
   - Allocations are $\mathcal{O}(1)$ pointer bumps, eliminating libc `ptmalloc` lock contention.
3. **`cairo` (`PdfEngineKind::Cairo`)**:
   - Cairo 2D graphics engine with front-loaded font caching.
   - Forces subset font embedding (larger files, slower throughput).

### JSON Ingestors (`--ingestor <engine>`)

1. **`simdjson` (`JsonIngestorKind::Simdjson`) — Default**:
   - SIMD-accelerated JSON parser with single-pass dictionary scanning, thread-local scratch buffer reuse, and lazy error formatting.
   - Parses banking payloads at >155,000 docs/second.
2. **`nlohmann` (`JsonIngestorKind::Nlohmann`)**:
   - Standard DOM-based JSON parser used for validation and compatibility.

---

## Build Instructions

### Prerequisites
- Linux x86_64
- C++23 capable compiler (GCC 13+ or Clang 17+)
- CMake 3.25+
- Ninja build system
- Third-party dependencies (ZLIB, OpenSSL, Cairo, nlohmann-json, simdjson, libharu) automatically resolved via system packages or CMake FetchContent
- OpenSSL >= 3.0 for SHA-256 hashing and signature tests (`libssl-dev`).
  CMS signing runs in the external signer service. A custom OpenSSL installation
  can be selected with `-DOPENSSL_ROOT_DIR=<path>`. The optional
  `tools/setup-openssl-3.3.sh` helper remains for the root Dockerfile, which still
  calls it; standalone generator builds can use system OpenSSL.
- Third-party libraries:
  - ZLIB (system package: `zlib1g-dev`)
  - Cairo 2D graphics (system package: `libcairo2-dev`)
  - JSON parsers: `simdjson` and `nlohmann-json` (system packages or auto-fetched via CMake `FetchContent`)
  - PDF backend: `libharu` (system package `libhpdf-dev` or auto-fetched via CMake `FetchContent`)

### Build Presets (`CMakePresets.json`)

CMake presets provide reproducible configuration and compilation for both Debug and Release environments.

#### Debug Build (Recommended for development & debugging)
Outputs to `build/debug/` with full debug symbols:
```bash
# Configure
cmake --preset debug

# Build all targets
cmake --build --preset debug -j
```

#### Release Build (Recommended for benchmarking & production deployment)
Outputs to `build/release/` with optimizations enabled:
```bash
# Configure
cmake --preset release

# Build all targets
cmake --build --preset release -j
```


#### Manual CMake Invocation (Fallback without presets)
```bash
# Debug
cmake -B build/debug -S . -DCMAKE_BUILD_TYPE=Debug -G Ninja
cmake --build build/debug -j

# Release
cmake -B build/release -S . -DCMAKE_BUILD_TYPE=Release -G Ninja
cmake --build build/release -j
```

### Build Artifacts
Artifacts are emitted into `build/<preset>/` (e.g. `build/debug/` or `build/release/`):
- `libnordiska_pdf_generator_c_api.so`: Exported C ABI shared library for .NET P/Invoke.
- `pdf_generator`: Standalone CLI worker.
- `nordiska_*_tests`: CTest unit test executables.

---

## Integration Contracts

### C ABI Public Interface (`pdf_generator_c_api.h`)

Exported C functions for host interop:

```c
#include "nordiska/c_api/pdf_generator_c_api.h"

// Synchronous generation for a single customer batch
int nordiska_pdf_v1_generate_customer_batch(
    const uint8_t* json_utf8,
    size_t json_length,
    nordiska_pdf_delivery_callback callback,
    void* user_data);

// Thread-local diagnostic retrieval
const char* nordiska_pdf_v1_get_last_error(void);
const char* nordiska_pdf_v1_status_name(int status_code);
```

The C API uses Libharu by default. Set `NORDISKA_PDF_ENGINE=native`,
`NORDISKA_PDF_ENGINE=haru` (or `libharu`), or `NORDISKA_PDF_ENGINE=cairo`
before generation to compare engines without rebuilding the library. Values
are case-insensitive; an unset or empty value retains Libharu. Unknown values
return invalid argument with no delivery callback. Signing configuration is
independent of the selected renderer.

#### V2 API (Future Preparation — Not Yet in Production)

> **Note:** The V2 C API (`nordiska_pdf_v2_generate_customer_batch` and
> `nordiska_pdf_v2_generate_customer_batch_instrumented`) is **preparation for
> future adoption** and is **not yet used in production or by the .NET worker**.
> Its main purpose is to eliminate duplicate hashing in the rest of the
> application: because the native generator already computes the SHA-256 digest
> of each complete delivered PDF (including any inserted signature), V2 exposes
> that checksum directly (`sha256_hex`, 64 lowercase hex characters + NUL
> terminator on `nordiska_pdf_document_view_v2`) so host callers do not need to
> re-hash PDF bytes after delivery.

Existing V1 functions and struct layouts remain completely unchanged for full
backward compatibility. Status codes, environment configuration, atomic delivery
and `nordiska_pdf_v1_get_last_error` are shared between V1 and V2.

Both entry points are stateless: each call resolves the current environment
configuration, constructs its own generator components and releases them when
the call returns. Concurrent calls generate independent customer batches.
There is no caller-owned generator handle or create/destroy lifecycle.

Timing collection is explicit. The normal V1 and V2 entry points pass no timing
collector, so the pipeline skips profiling clock reads and accumulation. Use
`nordiska_pdf_v1_generate_customer_batch_instrumented` or
`nordiska_pdf_v2_generate_customer_batch_instrumented` to collect phase timings
while using the same generation, validation and delivery code. V2 adds the
artifact hash field to the callback view.

After an instrumented call returns, copy its measurements with
`nordiska_pdf_v1_get_last_timing`; this shared helper serves both versions.
It returns the latest completed instrumented call's timings on the calling
thread. Normal calls leave that snapshot intact. A fresh thread sees zeros;
an instrumented failure reports work recorded before failure, or zeros when
arguments fail validation. Totals cover the native pipeline and exclude
configuration, component construction and callback delivery. Nested
instrumented calls have separate local collectors; each publishes its snapshot
when it returns. No environment variable is needed to choose instrumentation.

#### Status Codes (`nordiska_pdf_status`):

- `0`: `NORDISKA_PDF_OK` — Generation succeeded; delivery callback invoked.
- `1`: `NORDISKA_PDF_INVALID_ARGUMENT` — Null buffer, zero length, or null callback.
- `2`: `NORDISKA_PDF_INVALID_INPUT` — JSON parse or domain validation error.
- `3`: `NORDISKA_PDF_CALLBACK_FAILED` — Host callback rejected the completed batch.
- `4`: `NORDISKA_PDF_INTERNAL_ERROR` — PDF rendering or layout construction failure.
- `5`: `NORDISKA_PDF_RESOURCE_LIMIT_EXCEEDED` — Payload exceeds 32 MB limit.
- `6`: `NORDISKA_PDF_OUT_OF_MEMORY` — Memory allocation failed.
- `7`: `NORDISKA_PDF_SIGNING_FAILED` — Document signing failure (batch aborted, zero callbacks).

#### Memory Contract:
- **Synchronous execution**: Executes entirely on the caller's thread (zero thread hopping).
- **Borrowed memory**: Input JSON buffer is borrowed; native code never retains pointers after return.
- **Delivery callback**: Exactly once on batch completion. The callback borrows `nordiska_pdf_batch_view`. All PDF byte views are deallocated immediately upon callback return.

---

## CLI Usage

```bash
./build/debug/pdf_generator [OPTIONS] [INPUT_JSON]
# or for release:
./build/release/pdf_generator [OPTIONS] [INPUT_JSON]
```

### Options:
- `-i, --input <path>`: Path to input JSON payload (or `-` for stdin).
- `-o, --output <path>`: Destination directory or file path.
- `-r, --renderer <native|haru|cairo>`: Rendering engine (default: `haru`).
- `-e, --ingestor <simdjson|nlohmann>`: JSON ingestor (default: `simdjson`).
- `--no-compression`: Disable Flate stream compression for maximum rendering speed.
- `--compression <bool>`: Enable or disable stream compression (default: `true`).
- `--signing`: Call the PDF signer service via Unix domain socket and embed its CMS hex in generated documents (default: `false`).
- `--signature-capacity <n>`: Reserved CMS hex characters (default: `8192`).
- `-q, --quiet`: Suppress progress messages, only report errors.
- `-v, --verbose`: Print detailed execution summary and elapsed timing.
- `--json-summary`: Output machine-readable JSON summary to stdout.

### Examples:

```bash
# Generate batch with dedicated Native engine (fastest)
./build/release/pdf_generator -i docs/golden_customer_batch_sample.json -o output/ --renderer native --no-compression

# Process piped payload from stdin with JSON output summary
cat input.json | ./build/release/pdf_generator -o output/ --json-summary
```

---

## 7. Testing & Code Quality

For interactive, unsigned JSON-to-PDF fuzzing with AFL++, see
[tests/fuzz/README.md](tests/fuzz/README.md). Fuzz targets are opt-in and are not
included in normal builds.

```bash
# 1. Format native C++ code
./tools/format-native.sh

# 2. Verify formatting without modifications (CI check)
./tools/check-format.sh

# 3. Run automated CTest test suite with presets
ctest --preset debug
ctest --preset release

# Or run directly against specific build directory
ctest --test-dir build/debug --output-on-failure
ctest --test-dir build/release --output-on-failure
```
