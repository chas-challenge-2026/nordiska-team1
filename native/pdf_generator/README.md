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
│   ├── delivery/c_api/           # C89 ABI boundary (pdf_generator_c_api.h)
│   ├── diagnostics/              # Timing, metrics, and benchmark structures
│   ├── domain/                   # CustomerBatch, Document, PdfRenderingJob
│   ├── ingestion/                # JSON ingestor interface & factory
│   ├── layout/                   # Deterministic layout structures (DocumentLayout)
│   └── rendering/                # PDF rendering engine abstraction (PdfEngine)
├── src/                          # Subsystem implementations
│   ├── application/              # PdfGenerator 3-stage pipeline driver
│   ├── c_api/                    # C ABI implementation & boundary validation
│   ├── ingestion/                # simdjson (default) & nlohmann JSON parsers
│   ├── layout/                   # LayoutBuilder (typography, tables, flow)
│   └── rendering/                # Native (default), Haru (bump arena), Cairo
├── cli/                          # Standalone CLI binary (pdf_generator)
├── src/benchmark/                # Multi-threaded performance harness
├── tests/                        # CTest automated test suites
├── tools/                        # Code formatters & synthetic data generator
└── docs/                         # Golden customer batch specification
```

### The 3-Stage Pipeline

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

### Build Targets

```bash
# Configure (from native/pdf_generator directory)
cmake -B build -S . -DCMAKE_TOOLCHAIN_FILE=build/vcpkg_installed/x64-linux/scripts/buildsystems/vcpkg.cmake -G Ninja

# Build all targets
cmake --build build -j
```

### Build Artifacts
- `build/libnordiska_pdf_generator_c_api.so`: Exported C ABI shared library for .NET P/Invoke.
- `build/pdf_generator`: Standalone CLI worker.
- `build/pdf_generator_benchmark`: Multi-worker benchmarking tool.
- `build/nordiska_*_tests`: CTest unit test executables.

---

## 5. Integration Contracts

### C ABI Public Interface (`pdf_generator_c_api.h`)

Exported C functions for host interop:

```c
#include "nordiska/delivery/c_api/pdf_generator_c_api.h"

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

#### Memory Contract:
- **Synchronous execution**: Executes entirely on the caller's thread (zero thread hopping).
- **Borrowed memory**: Input JSON buffer is borrowed; native code never retains pointers after return.
- **Delivery callback**: Exactly once on batch completion. The callback borrows `nordiska_pdf_batch_view`. All PDF byte views are deallocated immediately upon callback return.

---

## 6. CLI Usage

```bash
./build/pdf_generator [OPTIONS] [INPUT_JSON]
```

### Options:
- `-i, --input <path>`: Path to input JSON payload (or `-` for stdin).
- `-o, --output <path>`: Destination directory or file path.
- `-r, --renderer <native|haru|cairo>`: Rendering engine (default: `haru`).
- `-e, --ingestor <simdjson|nlohmann>`: JSON ingestor (default: `simdjson`).
- `--no-compression`: Disable Flate stream compression for maximum rendering speed.
- `--compression <bool>`: Enable or disable stream compression (default: `true`).
- `-q, --quiet`: Suppress progress messages, only report errors.
- `-v, --verbose`: Print detailed execution summary and elapsed timing.
- `--json-summary`: Output machine-readable JSON summary to stdout.

### Examples:

```bash
# Generate batch with dedicated Native engine (fastest)
./build/pdf_generator -i docs/golden_customer_batch_sample.json -o output/ --renderer native --no-compression

# Process piped payload from stdin with JSON output summary
cat input.json | ./build/pdf_generator -o output/ --json-summary
```

---

## 7. Testing & Code Quality

In compliance with [`AGENTS.md`](AGENTS.md), formatting checks and automated tests must pass before every commit:

```bash
# 1. Format native C++ code
./tools/format-native.sh

# 2. Verify formatting without modifications (CI check)
./tools/check-format.sh

# 3. Run automated CTest test suite
ctest --test-dir build --output-on-failure
```
