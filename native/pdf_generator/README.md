# Nordiska Native Document Generator

A high-performance, standalone C++23 document generation engine. It renders PDF reports (such as annual tax summaries and account statements) using Libharu as its primary engine, exposed as an unmanaged C-compatible shared library for direct invocation from callers like .NET (C# P/Invoke).

> **Warning / Status (Last updated: 2026-09-10):**  
> **This README should be assumed to be out of date.** This component is under heavy and rapid development. Architecture, interfaces, and patterns are implicit in the codebase and evolve continuously. The code and tests are the authoritative source of truth; this document is provided solely as high-level context.

---

## Current Architecture & Structure

The codebase is organized under `include/nordiska/` and `src/`:

| Directory | Responsibility |
| :--- | :--- |
| `domain/` | Canonical business structs (`Report`, `Transaction`) and validation |
| `application/` | `GenerateDocuments` pipeline coordinator and batch orchestration |
| `ports/` | Abstract interfaces (renderers, output destinations, byte sinks) |
| `adapters/input/` | JSON parsing and input validation via `nlohmann/json` |
| `adapters/output/` | Output destinations (completion callbacks, memory buffers, files) |
| `adapters/renderers/` | PDF renderer facade and private Libharu / Cairo rendering engines |
| `composition/` | Dependency wiring and default engine selection |
| `c_api/` | Public external C ABI exports (header under `delivery/c_api/`) |
| `diagnostics/` | Performance metrics, timing, and benchmark instrumentation |
| `cli/`, `benchmark/` | Standalone CLI tool and synthetic benchmarking harness |

### Runtime Pipeline Flow

1. **Invocation:** Caller invokes the C API once per customer, passing a UTF-8 JSON payload containing all report objects for that customer and a completion callback ([`document_c_api.h`](include/nordiska/delivery/c_api/document_c_api.h)). Scoping each call strictly to a single customer prevents mixing customer data across batches.
2. **Input Parsing:** The input adapter parses and validates JSON into canonical `Report` objects.
3. **Application Orchestration:** `GenerateDocuments` validates requests and schedules rendering across worker threads.
4. **Layout & Pagination:** `PdfRenderer` calculates page layout, margins, headers, and transaction tables.
5. **PDF Rendering:** The underlying engine (Libharu / Cairo) writes the binary `%PDF-1.3` document stream.
6. **Delivery:** The output destination streams completed PDF bytes chunk-by-chunk directly into the caller's callback.

### Swappable PDF Engines

The rendering layer isolates low-level library specifics behind the `PdfRenderer` presentation facade:
* **Libharu (`libharu`):** The primary production engine. It is significantly faster and has a minimal memory footprint, but supports fewer styling and advanced graphical options. Ideal for high-volume tabular statements and reports.
* **Cairo (`cairo`):** An alternative engine with richer 2D vector graphics capabilities, but higher CPU and allocation overhead. Useful for complex layouts, benchmarks, and comparison.

Engines can be swapped at the composition layer without affecting the domain model or the external C ABI.

### Performance & SIMD JSON Roadmap

* **Current Bottleneck:** Benchmarks show that overall generation latency is dominated by JSON deserialization (currently using `nlohmann/json`), rather than PDF rendering.
* **Roadmap:** An input adapter interface will be introduced to allow swapping the parsing implementation to high-throughput parsers like **`simdjson`**, maximizing throughput under high-volume batch loads.

---

## External C API Usage

External callers (like the .NET `NativePdfGenerator` service) interact with the library through the C ABI declared in [`include/nordiska/delivery/c_api/document_c_api.h`](include/nordiska/delivery/c_api/document_c_api.h).

### Function Signature

```c
NORDISKA_DOCUMENT_API int nordiska_document_generate_json(
    const uint8_t* json_utf8,
    size_t json_length,
    nordiska_document_callback callback,
    void* callback_context,
    char* error_buffer,
    size_t error_buffer_length
);
```

### Callback Signature

When generation completes for each document, native code invokes the caller-provided callback:

```c
typedef int (*nordiska_document_callback)(
    const uint8_t* bytes,          // Pointer to raw PDF byte stream
    size_t length,                 // Number of bytes
    size_t document_index,         // 0-based document index in batch
    void* context                  // Caller-supplied passthrough pointer
);
```
* Return `0` from the callback to indicate success.
* Return non-zero to abort generation; native code will halt and report `NORDISKA_DOCUMENT_CALLBACK_FAILED`.

### Status Codes

| Code | Name | Description |
| :--- | :--- | :--- |
| `0` | `NORDISKA_DOCUMENT_OK` | Generation succeeded |
| `1` | `NORDISKA_DOCUMENT_INVALID_ARGUMENT` | Null pointer or invalid argument passed |
| `2` | `NORDISKA_DOCUMENT_INVALID_INPUT` | Malformed JSON or schema validation failure |
| `3` | `NORDISKA_DOCUMENT_CALLBACK_FAILED` | Caller callback returned non-zero |
| `4` | `NORDISKA_DOCUMENT_INTERNAL_ERROR` | Unexpected engine or rendering exception |

---

## Building and Testing

### Prerequisites
* CMake 3.28+
* Ninja build system
* C++23 compatible compiler (GCC 13+, Clang 17+, or MSVC 2022)
* [vcpkg](https://vcpkg.io) installed with `VCPKG_ROOT` environment variable exported

### Build Commands

From the `native/pdf_generator` directory:

```bash
# Configure with CMake preset
cmake --preset default

# Compile all targets (libraries, executables, tests)
cmake --build --preset default

# Run all automated tests
ctest --test-dir build --output-on-failure
```

### Generated Artifacts
* `libnordiska_document_c_api.so` (`.dll` on Windows): The shared library for external callers.
* `pdf_generator`: Standalone CLI tool for generating PDFs from input files directly in terminal.
* `pdf_generator_benchmark`: Benchmarking harness for measuring parsing, rendering, and throughput.

---

## Testing & Synthetic Benchmark Data

* **Automated Unit & Integration Tests:** Run via `ctest --test-dir build --output-on-failure`. Tests cover JSON schema parsing, Haru rendering output, byte sinks, and C API boundary callbacks.
* **Synthetic Data Generator:** A standalone Python tool in `tools/synthetic-input-generator/` creates deterministic, realistic test datasets (with accounts, transactions, and tax lines) for stress-testing and benchmarking:
  ```bash
  # Generate synthetic workload datasets (written to generated/)
  ./tools/synthetic-input-generator/generate_all.sh

  # Run benchmark against generated data
  ./build/pdf_generator_benchmark ./tools/synthetic-input-generator/generated/realistic
  ```

---

## Formatting and Code Standards

Before committing changes to native code, run the clang-format scripts:

```bash
# Auto-format all C++ source files
./tools/format-native.sh

# Verify formatting compliance
./tools/check-format.sh
```
