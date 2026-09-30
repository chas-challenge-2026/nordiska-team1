# Nordiska Native PDF Generator

C++23 module for generating customer PDF reports (account statements, annual summaries, and tax reports).
Designed for direct FFI / P-Invoke integration from .NET services and standalone native CLI execution on Linux.

---


## Architecture & Subsystems

```text
native/pdf_generator/
├── include/nordiska/             # Public C++ interface headers
│   ├── application/              # Orchestration (PdfGenerator, GeneratorConfig)
│   ├── c_api/                    # C89 ABI boundary (pdf_generator_c_api.h)
│   ├── diagnostics/              # Timing, metrics, and benchmark structures
│   ├── domain/                   # CustomerBatch, Document, PdfRenderingJob
│   ├── ingestion/                # JSON ingestor interface & factory
│   ├── layout/                   # Deterministic layout structures (DocumentLayout)
│   ├── rendering/                # PDF rendering engine abstraction (PdfEngine)
│   └── signing/                  # Digest signing interface & PDF preparation
├── src/                          # Subsystem implementations
│   ├── application/              # PdfGenerator pipeline driver
│   ├── c_api/                    # C ABI implementation & boundary validation
│   ├── ingestion/                # simdjson (default) & nlohmann JSON parsers
│   ├── layout/                   # LayoutBuilder (typography, tables, flow)
│   ├── rendering/                # Native (default), Haru (bump arena), Cairo
│   └── signing/                  # C signer adapter, PDF preparation & hashing
├── cli/                          # Standalone CLI binary (pdf_generator)
├── src/benchmark/                # Multi-threaded performance harness
├── tests/                        # CTest automated test suites
├── tools/                        # Code formatters, synthetic data generator & OpenSSL setup
└── docs/                         # Golden customer batch specification
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
   - Generates PositionedLine and PositionedText
         │
         ▼
   DocumentLayout (deterministic coordinate geometry)
         │
         ▼  [3. Rendering Stage]
   PdfEngine (Native / Libharu / Cairo)
   - Compiles binary PDF 1.4 streams
         │
         ▼  [4. Optional Signing Stage]
   PdfSigner (Stub / PKCS#7)
   - Per-document signing dispatch
   - Strict all-or-nothing failure guarantee
         │
         ▼
GeneratedPdfs / C ABI Delivery Callback
```

---

## Pluggable Engines & Ingestors

### PDF Rendering Engines (`--renderer <engine>`)

1. **`native` (`PdfEngineKind::Native`) — Default Recommended**:
   - Zero-dependency direct PDF 1.4 compiler.
   - Formats Core-14 PostScript Type 1 Helvetica and Helvetica-Bold font dictionaries, text matrices (`BT`, `Tf`, `Tm`, `Tj`, `ET`), and vector line paths.
   - Zero-allocation numeric formatting via `<charconv>` (`std::to_chars`).
   - Produces the smallest file size (1.67 KB compressed) and fastest throughput (>44k docs/s).
2. **`haru` (`PdfEngineKind::Libharu`)**:
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
- OpenSSL >= 3.2.0 (Required for CMS SignedData digest signing with `CMS_final_digest`)
  - *Ubuntu / Debian LTS Notice*: Most LTS distributions ship OpenSSL 3.0.x by default. On Ubuntu 22.04/24.04 or Debian 12 developer workstations, run the provided local setup script to build and install OpenSSL 3.3.2 into `~/.local/openssl-3.3/` (isolated, non-root, no system changes):
    ```bash
    ./tools/setup-openssl-3.3.sh
    ```
    CMake automatically auto-detects this path when configuring.
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
- `pdf_generator_benchmark`: Multi-worker benchmarking tool.
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
- `--signing`: Call the C signer and embed its CMS hex in generated documents (default: `false`).
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
