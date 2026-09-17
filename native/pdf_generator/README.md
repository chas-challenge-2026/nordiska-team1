# Nordiska Native PDF Generator

High-performance native C++23 batch PDF generator for Nordiska banking documents (monthly account statements and annual tax summaries).

This directory is a standalone native component designed to be used in two ways:
1. **As a shared C library (`libnordiska_pdf_generator_c_api.so`)**: Called directly by the .NET backend via P/Invoke.
2. **As a standalone CLI executable (`pdf_generator`)**: For batch file generation, offline generation pipelines, and testing.

---

## Prerequisites

- **OS:** Linux x86_64
- **Compiler:** GCC 13+ or Clang 17+ (with C++23 support)
- **Build Tools:** CMake 3.25+, Ninja, `pkg-config`
- **System Libraries:** `libcairo2-dev`
- **Package Manager:** `vcpkg` (dependencies: `simdjson`, `nlohmann-json`, `libharu`, `zlib`)

On Debian/Ubuntu:

```bash
sudo apt-get update
sudo apt-get install -y build-essential cmake ninja-build pkg-config libcairo2-dev
```

---

## Building the Project

The project uses CMake with vcpkg in manifest mode (`vcpkg.json`). Dependencies are resolved and compiled automatically during the initial configure step.

From the `native/pdf_generator` directory:

```bash
# 1. Configure the build with Ninja and vcpkg
cmake -B build -S . -DCMAKE_TOOLCHAIN_FILE=build/vcpkg_installed/x64-linux/scripts/buildsystems/vcpkg.cmake -G Ninja

# 2. Compile all targets
cmake --build build -j
```

### Build Outputs

After compiling, the `build/` directory will contain:

| Binary | Description |
|---|---|
| `build/pdf_generator` | Standalone CLI tool for processing customer JSON files |
| `build/libnordiska_pdf_generator_c_api.so` | C ABI shared library consumed by the .NET service |
| `build/pdf_generator_benchmark` | Multi-threaded performance harness |
| `build/nordiska_*_tests` | Automated CTest test suites |

---

## Quickstart: Running the CLI

Generate PDFs from an input customer JSON batch:

```bash
# Generate PDFs from the sample batch into the output directory
./build/pdf_generator -i docs/golden_customer_batch_sample.json -o output/

# Run with verbose timing breakdown
./build/pdf_generator -i docs/golden_customer_batch_sample.json -o output/ -v
```

The command parses the customer batch, lays out each document, renders PDF files to `output/` (e.g. `output/account1_statement.pdf`, `output/account1_tax.pdf`), and prints a summary.

### CLI Options

| Flag | Argument | Default | Description |
|---|---|---|---|
| `-i, --input` | `<path>` | Stdin | Path to customer batch JSON file (or `-` for stdin) |
| `-o, --output` | `<path>` | `.` | Directory to write generated PDF files |
| `-r, --renderer` | `<engine>` | `haru` | Rendering engine: `native`, `haru`, or `cairo` |
| `-e, --ingestor` | `<engine>` | `simdjson` | JSON parser: `simdjson` or `nlohmann` |
| `--no-compression` | — | Disabled | Disable Flate stream compression for maximum speed |
| `--compression` | `<bool>` | `true` | Enable or disable stream compression |
| `-v, --verbose` | — | Disabled | Print detailed generation timings |
| `-q, --quiet` | — | Disabled | Suppress progress messages, only report errors |
| `--json-summary` | — | Disabled | Output machine-readable JSON summary to stdout |

### Piped / Streamed Input

The CLI supports reading directly from standard input:

```bash
cat docs/golden_customer_batch_sample.json | ./build/pdf_generator -o output/ --json-summary
```

---

## Generating Synthetic Test Data

The generator is designed to process atomic customer batches matching the schema in `docs/golden_customer_batch_sample.json`.

To generate large, realistic test datasets representing Swedish banking customers across full calendar years, use the synthetic data generator tool:

```bash
# Generate a pool of 100 realistic customer batches
python3 tools/synthetic-input-generator/generate_data.py --customers 100 --clean

# Generate 500 customers for custom testing
python3 tools/synthetic-input-generator/generate_data.py --customers 500 --output generated/pool_500 --clean
```

Generated datasets are written to `tools/synthetic-input-generator/generated/pool_100/`.

For detailed information on Pareto distributions, transaction generation, and Swedish banking rules, see the [Synthetic Input Generator README](tools/synthetic-input-generator/README.md).

---

## Benchmarking Performance

To measure document generation throughput, multi-core scaling, and per-phase CPU latency without disk I/O bottlenecks:

```bash
# Default benchmark (loads 100 pre-generated customers automatically)
./build/pdf_generator_benchmark

# High-throughput test: 50,000 customers across 16 worker threads with phase instrumentation
./build/pdf_generator_benchmark --target-customers 50000 --workers 16 --instrumented

# Maximum throughput run using the dedicated Native engine
./build/pdf_generator_benchmark --target-customers 50000 --workers 16 --renderer native --no-compression --instrumented
```

For full benchmark CLI options, memory tracking metrics, and phase profiling details, see the [Benchmark Harness README](src/benchmark/README.md).

---

## Host Integration (C ABI)

External hosts (such as .NET via P/Invoke) interact with the generator through the exported C ABI defined in [`include/nordiska/delivery/c_api/pdf_generator_c_api.h`](include/nordiska/delivery/c_api/pdf_generator_c_api.h).

### Main Entry Point

```c
int nordiska_pdf_v1_generate_customer_batch(
    const uint8_t* json_utf8,
    size_t json_length,
    nordiska_pdf_delivery_callback callback,
    void* user_data);
```

### Integration Guarantees:
- **Stateless & Synchronous:** The call executes entirely on the caller's thread without background thread hopping or internal thread pools.
- **Borrowed Memory:** The host retains ownership of the input JSON buffer. Native code borrows the memory during parsing and retains zero references after the call returns.
- **Atomic Batch Contract:** Generation is all-or-nothing. On success, the host callback is invoked exactly once with the complete batch of PDF document views (`nordiska_pdf_batch_view`). All PDF buffers are released as soon as the callback completes.
- **Thread-Local Diagnostics:** If generation fails, detailed error messages can be retrieved on the calling thread via `nordiska_pdf_v1_get_last_error()`.

---

## Testing & Quality Assurance

Automated tests and code formatting checks must pass before every commit:

```bash
# 1. Run automated unit tests
ctest --test-dir build --output-on-failure

# 2. Format native C++ code
./tools/format-native.sh

# 3. Verify formatting (CI check)
./tools/check-format.sh
```

---

## Project Structure

```text
native/pdf_generator/
├── CMakeLists.txt                # CMake build definitions
├── vcpkg.json                    # Dependency manifest (simdjson, libharu, etc.)
├── cli/                          # Standalone CLI binary entry point (main.cpp)
├── include/nordiska/             # Public C++ headers
│   ├── application/              # Generator pipeline driver (PdfGenerator)
│   ├── delivery/c_api/           # C ABI boundary (pdf_generator_c_api.h)
│   ├── diagnostics/              # Metric collection and timing structures
│   ├── domain/                   # Batch, Document, and PdfRenderingJob models
│   ├── ingestion/                # JSON ingestor interface (JsonIngestor)
│   ├── layout/                   # Layout models and coordinate geometry
│   └── rendering/                # PDF rendering engine interface (PdfEngine)
├── src/                          # Implementation sources
│   ├── application/              # Orchestration logic
│   ├── benchmark/                # Benchmark harness (main.cpp, README.md)
│   ├── c_api/                    # C ABI implementation & boundary validation
│   ├── ingestion/                # simdjson and nlohmann ingestor implementations
│   ├── layout/                   # Layout builder (typography, tables, headers)
│   └── rendering/                # Native, Libharu (bump arena), Cairo engines
├── tests/                        # CTest unit tests (diagnostics, C ABI, ingestor)
├── tools/
│   ├── check-format.sh           # Clang-format verification script
│   ├── format-native.sh          # In-place clang-format runner
│   └── synthetic-input-generator/# Python synthetic customer data tool
└── docs/                         # Golden customer batch JSON reference
```
