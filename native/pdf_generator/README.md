# Nordiska Native PDF Generator

High-performance, standalone native C++23 module for batch-generating customer banking documents (account statements, annual summaries, and tax reports).

Designed for direct FFI / P-Invoke integration from .NET services and standalone native CLI execution on Linux.

---

## 1. Key Performance Highlights

Benchmarked on 16 threads processing 50,000 customers (170,500 documents, 2,236,000 ledger transactions) with zero disk I/O:

| Metric | Dedicated Native Engine | Libharu Engine | Cairo Engine |
|---|---|---|---|
| **Peak Throughput (docs/sec)** | **44,401 docs/s** | 31,185 docs/s | 711 docs/s |
| **Document Rate (customers/sec)**| **13,021 cust/s** | 9,145 cust/s | 208 cust/s |
| **PDF Render CPU Latency** | **0.206 ms / doc** | 0.364 ms / doc | 22.45 ms / doc |
| **Output Document Size** | **1.67 KB** (compressed) | 3.62 KB (compressed) | 22.10 KB |
| **Total Batch Time (50k customers)**| **3.84 s** | 5.47 s | 239.8 s |

---

## 2. Architecture & Subsystems

The generator strictly adheres to [`AGENTS.md`](AGENTS.md): modular architecture, explicit lifetimes, strong typing, and no C++ types crossing boundary interfaces.

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
├── tools/                        # Code formatters & synthetic data generator
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

## 3. Pluggable Engines & Ingestors

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

## 4. Build Instructions

### Prerequisites
- Linux x86_64
- C++23 capable compiler (GCC 13+ or Clang 17+)
- CMake 3.25+
- Ninja build system
- vcpkg dependencies (automatically resolved via `vcpkg.json` manifest)
- `$VCPKG_ROOT` environment variable exported (e.g. `export VCPKG_ROOT=$HOME/vcpkg`)

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
cmake -B build/debug -S . -DCMAKE_BUILD_TYPE=Debug -DCMAKE_TOOLCHAIN_FILE="$VCPKG_ROOT/scripts/buildsystems/vcpkg.cmake" -G Ninja
cmake --build build/debug -j

# Release
cmake -B build/release -S . -DCMAKE_BUILD_TYPE=Release -DCMAKE_TOOLCHAIN_FILE="$VCPKG_ROOT/scripts/buildsystems/vcpkg.cmake" -G Ninja
cmake --build build/release -j
```

### Build Artifacts
Artifacts are emitted into `build/<preset>/` (e.g. `build/debug/` or `build/release/`):
- `libnordiska_pdf_generator_c_api.so`: Exported C ABI shared library for .NET P/Invoke.
- `pdf_generator`: Standalone CLI worker.
- `pdf_generator_benchmark`: Multi-worker benchmarking tool.
- `nordiska_*_tests`: CTest unit test executables.

---

## 5. Integration Contracts

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

## 6. CLI Usage

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

---

## 8. Interactive Debugging with GDB & LLDB

Binaries compiled under the `debug` preset include full DWARF debug symbols and frame pointers without aggressive compiler optimizations.

### Debugging the Standalone CLI

#### GDB
```bash
# Launch CLI under GDB with arguments
gdb --args build/debug/pdf_generator -i docs/golden_customer_batch_sample.json -o output/ --renderer native -v

# Common GDB commands:
(gdb) break main                                          # Break at program entry point
(gdb) break nordiska::application::PdfGenerator::generate # Break at core orchestrator
(gdb) break nordiska::layout::LayoutBuilder::build        # Break at layout calculation
(gdb) run                                                 # Start execution (r)
(gdb) next                                                # Step over (n)
(gdb) step                                                # Step into (s)
(gdb) print job.customer_name                             # Inspect variables (p)
(gdb) info locals                                         # Print all local variables
(gdb) backtrace                                           # Print stack trace upon crash (bt)
(gdb) continue                                            # Resume execution (c)
```

#### LLDB
```bash
# Launch CLI under LLDB with arguments
lldb -- build/debug/pdf_generator -i docs/golden_customer_batch_sample.json -o output/ --renderer native -v

# Common LLDB commands:
(lldb) breakpoint set --name main
(lldb) breakpoint set --name nordiska::application::PdfGenerator::generate
(lldb) run               # Start execution (r)
(lldb) thread step-over  # Step over (n)
(lldb) thread step-in    # Step into (s)
(lldb) frame variable    # Inspect local variables (fr v)
(lldb) thread backtrace  # Print call stack (bt)
(lldb) thread continue   # Resume execution (c)
```

### Debugging Unit Test Failures

To isolate and step through a specific test suite or failure:
```bash
# GDB
gdb --args build/debug/nordiska_pdf_signing_tests
(gdb) run
(gdb) backtrace

# LLDB
lldb -- build/debug/nordiska_pdf_signing_tests
(lldb) run
(lldb) thread backtrace
```

### Debugging C API Shared Library Interop

When debugging host integration (.NET P/Invoke or test harnesses) against `libnordiska_pdf_generator_c_api.so`:
```bash
# Run C API unit test suite directly under GDB
gdb --args build/debug/nordiska_pdf_generator_c_api_tests

# Or attach GDB to an existing host process loading the library
gdb -p <PID>
(gdb) sharedlibrary libnordiska_pdf_generator_c_api.so
(gdb) break nordiska_pdf_v1_generate_customer_batch
(gdb) continue
```

---

## 9. PDF Signature Preparation and Signing

See [PDF signing integration](docs/pdf_signing_integration.md) for capacity units,
the C signer dependency, error/ownership contracts, and current stub limitations.

