# Native PDF Generator — Fuzzing and Hardening Report

**Date**: 19 October 2026  
**Scope**: `native/pdf_generator` (JSON Ingestion, Layout Engine, PDF Renderers, Signing Pipeline, C ABI)  
**Status**: All suites passing (100% CTest success)

---

## 1. Executive Summary

A comprehensive fuzzing and robustness testing campaign was autonomously developed and executed against the `nordiska_pdf_generator` native codebase. Rather than relying on a single long-running fuzzing process, the testing suite was partitioned into three high-velocity, deterministic fuzzers designed to explore combinatorial settings, low-level binary PDF manipulation, and boundary contracts:

1. **Combinatorial Pipeline Fuzzer** (`nordiska_fuzz_combinatorial_pipeline`): Multi-dimensional test matrix across ingestors, rendering engines, compression settings, signing modes, and capacities.
2. **PDF Parser & Signature Slot Appender Fuzzer** (`nordiska_fuzz_pdf_signing_slot`): Systematic byte truncation and 15,000 mutation iterations on real engine-generated PDFs.
3. **C ABI Boundary & Concurrency Fuzzer** (`nordiska_fuzz_c_api`): Argument boundary checks, callback rejections, environment variable permutations, V2 SHA-256 independent verification, and 8-thread concurrent stress testing.

Through this campaign, **three critical bugs** were identified and resolved, including a memory corruption / use-after-free issue in the Libharu bump allocator.

---

## 2. Defects Identified and Resolved

### 2.1. Critical: `HaruBumpArena` Use-After-Free on Large Batches
* **Location**: `src/rendering/haru_pdf_engine.cpp` (`HaruBumpArena`)
* **Impact**: Heap corruption / segmentation faults on large documents (>512 KB)
* **Root Cause**:
  `HaruBumpArena` previously backed its bump allocation with a single `std::vector<uint8_t> buffer` initialized to 512 KB. When a document had enough transactions/pages to exceed 512 KB, `allocate()` called `buffer.resize(...)`. In C++, resizing a vector beyond its capacity reallocates the underlying heap buffer, invalidating all previously returned pointers. Because Libharu retained raw pointers to its root `HPDF_Doc`, pages, fonts, and dictionaries within this arena, subsequent Libharu operations accessed freed memory.
* **Resolution**:
  Replaced the single reallocating vector with a chunked arena: `std::vector<std::vector<uint8_t>> chunks`. When the current chunk is exhausted, a new chunk is appended. Because `std::vector` stores heap buffers via internal pointers, existing chunks are never reallocated or moved, guaranteeing pointer stability for all allocated objects until `reset()`.

### 2.2. Medium: `append_float_2` Buffer Overflow / Uninitialized Content Stream Bytes
* **Location**: `src/rendering/native_pdf_engine.cpp` (`append_float_2`)
* **Impact**: Corrupted PDF content streams or uninitialized memory leaks when formatting extreme floating-point coordinates
* **Root Cause**:
  `append_float_2` used a fixed 32-byte stack buffer `char buf[32]`. Formatting large floats ($>10^{30}$) in fixed 2-decimal notation requires more than 32 characters, causing `std::to_chars` to return `std::errc::value_too_large`. In accordance with the C++ standard, `to_chars` sets `ptr = last` upon error. The code did not check `ec == std::errc{}` and unconditionally appended `ptr - buf` (32 bytes of uninitialized buffer contents) into the PDF stream.
* **Resolution**:
  Expanded the buffer to 64 bytes (sufficient for any single-precision IEEE 754 float in fixed notation) and added an explicit check: if `ec == std::errc{}`, append the formatted slice; otherwise, safely fall back to `"0.00"`.

### 2.3. Low/Differential: `customer_id` Unsigned 64-bit Integer Wrapping
* **Location**: `src/ingestion/json_ingestor.cpp` (`NlohmannIngestorImpl::ingest`)
* **Impact**: Differential rejection between Simdjson and Nlohmann for valid large customer IDs
* **Root Cause**:
  `NlohmannIngestorImpl` parsed `customer_id` by converting it to `int64_t`. Unsigned customer IDs between `INT64_MAX + 1` and `UINT64_MAX` wrapped to negative numbers, triggering `if (raw_cust_id <= 0)` and falsely reporting `"customer_id must be greater than zero"`. `SimdjsonIngestorImpl` correctly supported the full `uint64_t` range.
* **Resolution**:
  Updated `NlohmannIngestorImpl` to check `cust_id_val.is_number_unsigned()` first and retrieve `uint64_t` directly, aligning both ingestors.

---

## 3. Fuzzing Test Suites Overview

### 3.1. Combinatorial Pipeline Fuzzer (`nordiska_fuzz_combinatorial_pipeline`)
Exercises the complete matrix of configuration combinations:

| Dimension | Values Tested |
| :--- | :--- |
| **Ingestor** | `Simdjson`, `Nlohmann` |
| **Engine** | `Libharu`, `Cairo`, `Native` |
| **Compression** | Enabled (`FlateDecode`), Disabled (Raw streams) |
| **Signing** | Unsigned, Signed (Valid CMS, Odd Hex, Non-Hex, Empty, Oversized, Error) |
| **Capacity** | `8192`, `1024`, `65536`, `128`, `0`, `1023` (Odd) |
| **Pagination** | 0, 1, 10, 80, 300+ transactions (stressing table headers at $y=54.0$ and page limits) |
| **Typography** | Swedish (`åäöÅÄÖ`), CP1252 symbols (`€`, `™`, `—`, `–`, `•`), unmapped Unicode, ASCII control |

* **Corpus & Mutations**: Loaded golden batch samples and ran 4,000 raw bit/byte mutation iterations across all engines, verifying invariant adherence on successful outputs and clean rejections on malformed inputs.
* **Differential Testing**: Validated that `Simdjson` and `Nlohmann` yield identical document counts, customer IDs, and validity outcomes on matching payloads.

### 3.2. PDF Parser & Signature Slot Appender Fuzzer (`nordiska_fuzz_pdf_signing_slot`)
Directly fuzzes the PDF structure parser in `signature_slot_appender.cpp`:

* **Sample Generation**: Baseline PDFs rendered across `Libharu`, `Cairo`, and `Native`.
* **Truncation Sweeps**: Truncated PDFs at every byte offset from 0 to EOF to ensure graceful error handling on truncated streams.
* **Mutation Sweeps**: Executed 15,000 random mutations (bit flips, byte swaps, deletions, keyword corruptions) across all 3 engine outputs.
* **Contract Verification**:
  * Clean failure on malformed trailers, missing `/Root`, missing `/Size`, or invalid `/Prev`.
  * Rejection of invalid CMS signatures (odd hex, non-hex, empty, oversized).
  * Double-signing prevention on already signed documents.
  * Direct boundary testing of signature slot ranges and delimiters (`<` and `>`).

### 3.3. C ABI Boundary & Concurrency Fuzzer (`nordiska_fuzz_c_api`)
Validates stability at the C ABI boundary:

* **Boundary Arguments**: Null buffer pointers, 0-length inputs, null callbacks, and payload sizes exceeding `kMaxJsonPayloadBytes` (32 MB).
* **Return Code Contracts**: Tested callback rejection codes (e.g. status 42) and verified that thread-local diagnostic storage (`nordiska_pdf_v1_get_last_error`) records the rejection code.
* **Environment Configuration**: Verified case-insensitive engine selection (`NORDISKA_PDF_ENGINE=native|cairo|libharu|invalid`).
* **V2 Artifact Integrity**: Verified that `view->sha256_hex` matches an independent OpenSSL SHA-256 calculation byte-for-byte.
* **Concurrency**: 8 worker threads executing parallel batch generations to verify zero thread-local state leakage or data races.

---

## 4. Verification and Execution Instructions

All test suites are integrated into CMake and CTest.

### Build and Run Tests

```bash
cd native/pdf_generator

# Build all targets in Debug preset
cmake --build build/debug

# Run all 7 test suites via CTest
ctest --test-dir build/debug --output-on-failure
```

### Standalone Fuzzer Invocation

The fuzzers can also be executed directly:

```bash
# 1. Combinatorial Pipeline Fuzzer
./build/debug/tests/fuzz/nordiska_fuzz_combinatorial_pipeline

# 2. PDF Parser & Signature Slot Appender Fuzzer
./build/debug/tests/fuzz/nordiska_fuzz_pdf_signing_slot

# 3. C ABI Boundary & Concurrency Fuzzer
./build/debug/tests/fuzz/nordiska_fuzz_c_api

# 4. Seed Replay Driver
./build/debug/tests/fuzz/nordiska_fuzz_json_to_pdf --require-success tests/fuzz/seeds/*.json
```

---

## 5. Test Results Summary

```text
Test project /home/jj/Projects/nordiska-team1/native/pdf_generator/build/debug
    Start 1: nordiska_pdf_signing_tests
1/7 Test #1: nordiska_pdf_signing_tests .............   Passed    0.10 sec
    Start 2: nordiska_json_ingestor_tests
2/7 Test #2: nordiska_json_ingestor_tests ...........   Passed    0.03 sec
    Start 3: nordiska_pdf_generator_c_api_tests
3/7 Test #3: nordiska_pdf_generator_c_api_tests .....   Passed    0.12 sec
    Start 4: nordiska_fuzz_seed_replay
4/7 Test #4: nordiska_fuzz_seed_replay ..............   Passed    0.01 sec
    Start 5: nordiska_fuzz_pdf_signing_slot
5/7 Test #5: nordiska_fuzz_pdf_signing_slot .........   Passed    1.12 sec
    Start 6: nordiska_fuzz_combinatorial_pipeline
6/7 Test #6: nordiska_fuzz_combinatorial_pipeline ...   Passed    2.53 sec
    Start 7: nordiska_fuzz_c_api
7/7 Test #7: nordiska_fuzz_c_api ....................   Passed    0.05 sec

100% tests passed, 0 tests failed out of 7
Total Test time (real) = 3.95 sec
```

