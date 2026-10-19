# Native PDF Generator — Comprehensive Hardening, Fuzzing & Pre-Merge Validation Report

**Date:** 19 October 2026  
**Target Branch:** `main`  
**Feature Branch:** `native/pdf_generator`  
**Scope:** `native/pdf_generator/` (Host C ABI, JSON Ingestion, Document Layout Engine, PDF Rendering Engines, In-Memory Cryptographic Signing Pipeline, Fuzzing Infrastructure)  
**Status:** **100% Tests Passing (11 / 11 CTest Suites Green)** — Ready for Merge to `main`

---

## 1. Executive Summary

As part of red-team adversarial stress testing, continuous fuzzing, and pre-merge hardening for the native PDF generator component (`native/pdf_generator/`) invoked via .NET P/Invoke (`Nordiska.Reporting.Worker`), the native pipeline was subjected to comprehensive combinatorial mutation, binary PDF parsing sweeps, raw PDF operator injection attacks, and multi-threaded concurrency stress.

### Key Achievements & Health Summary
- **Three Defects Discovered & Remediated:**
  1. **Critical (P1):** Heap use-after-free / dangling pointer vulnerability in `HaruBumpArena` (`src/rendering/haru_pdf_engine.cpp`) on documents exceeding 512 KB. Resolved via stable chunked arena storage.
  2. **Medium (P2):** Buffer overflow and uninitialized stream bytes in `append_float_2` (`src/rendering/native_pdf_engine.cpp`). Resolved via 64-byte expansion and explicit error fallback.
  3. **Low / Differential (P3):** `customer_id` unsigned 64-bit integer wrapping in `NlohmannIngestorImpl` (`src/ingestion/json_ingestor.cpp`). Resolved to support full `uint64_t` range matching `Simdjson`.
- **Elimination of Test Disk I/O:** Converted all sample artifact checks in `tests/pdf_signing_tests.cpp` to pure in-memory structural validation (`nordiska::test::is_valid_pdf_structure`), preventing dirty working directory artifacts (`signing_samples/`) from polluting version control.
- **Standards & Syntactic Conformance:** All valid PDFs generated across Native, Libharu, and Cairo engines pass strict Ghostscript verification (`gs -dNODISPLAY -dBATCH -dNOPAUSE -dPDFSTOPONERROR -dQUIET -`) with zero syntax or rendering warnings.
- **Two-Tier Test Suite Expansion:** Expanded CTest from the original 3 unit tests to **11 comprehensive suites** (4 deterministic adversarial suites, 4 continuous fuzzing/replay suites, and 3 baseline integration tests), all passing 100% across Release, Debug, and AddressSanitizer/UBSan profiles.

---

## 2. Defects Identified & Remediated

### 2.1. Critical (P1) — Heap Use-After-Free via Pointer Invalidation in `HaruBumpArena`
* **File:** `src/rendering/haru_pdf_engine.cpp` (`HaruBumpArena`)
* **Trigger:** Generating multi-page statements with large transaction volumes (>3,000 transactions) or complex multi-page layouts where cumulative Libharu allocations exceed 512 KB.
* **Mechanism:**
  `HaruBumpArena` originally stored its memory buffer in a single `std::vector<uint8_t> buffer` initialized to 512 KB. When allocations exceeded 512 KB, `allocate()` called `buffer.resize(...)`. In C++, resizing a vector beyond its capacity reallocates the underlying heap buffer and frees the previous block.
  Because Libharu stores raw C pointers across its internal document object graph (`HPDF_Doc`, pages, streams, catalog, dictionary entries), every pointer previously issued by `haru_arena_alloc()` became a dangling pointer referencing deallocated heap memory. Under .NET worker P/Invoke execution, this resulted in memory corruption, corrupted PDF generation, or sudden process termination (`SIGSEGV`).
* **Remediation:**
  Replaced the single reallocating vector with a chunked arena: `std::vector<std::vector<uint8_t>> chunks`. When the active chunk is exhausted, a new chunk is appended. Because `std::vector` stores heap buffers via internal pointers, existing chunks are never reallocated or moved, guaranteeing pointer stability for all allocated objects until `reset()`. Furthermore, `reset()` trims chunk retention (`if (chunks.size() > 4) chunks.resize(2);`) to prevent long-lived worker threads from retaining unbounded heap memory after traffic spikes.

### 2.2. Medium (P2) — `append_float_2` Buffer Overflow & Uninitialized Content Stream Bytes
* **File:** `src/rendering/native_pdf_engine.cpp` (`append_float_2`)
* **Trigger:** Formatting extreme floating-point coordinates ($>10^{30}$) in fixed 2-decimal notation.
* **Mechanism:**
  `append_float_2` used a fixed 32-byte stack buffer `char buf[32]`. Formatting large floats in fixed 2-decimal notation requires more than 32 characters, causing `std::to_chars` to return `std::errc::value_too_large`. In accordance with C++ standard specifications, `to_chars` sets `ptr = last` upon error. The code did not verify `ec == std::errc{}` and unconditionally appended `ptr - buf` (32 bytes of uninitialized stack buffer contents) into the PDF content stream.
* **Remediation:**
  Expanded the buffer to 64 bytes (sufficient for any single-precision IEEE 754 float in fixed notation) and added an explicit check: if `ec == std::errc{}`, append the formatted slice; otherwise, safely fall back to `"0.00"`.

### 2.3. Low / Differential (P3) — `customer_id` Unsigned 64-bit Integer Wrapping
* **File:** `src/ingestion/json_ingestor.cpp` (`NlohmannIngestorImpl::ingest`)
* **Trigger:** Customer IDs between `INT64_MAX + 1` and `UINT64_MAX` ($9,223,372,036,854,775,808$ to $18,446,744,073,709,551,615$).
* **Mechanism:**
  `NlohmannIngestorImpl` parsed `customer_id` by converting it to `int64_t`. Unsigned customer IDs exceeding `INT64_MAX` wrapped to negative numbers, triggering `if (raw_cust_id <= 0)` and falsely reporting `"customer_id must be greater than zero"`. In contrast, `SimdjsonIngestorImpl` supported the full `uint64_t` range, creating an ingestor discrepancy.
* **Remediation:**
  Updated `NlohmannIngestorImpl` to check `cust_id_val.is_number_unsigned()` first and retrieve `uint64_t` directly, aligning behavior across both ingestors.

---

## 3. Architecture Comparison: Upstream vs. Local `HaruBumpArena`

During independent auditing, two solutions to the `HaruBumpArena` reallocation bug were implemented:

| Feature / Metric | Upstream (`b865dfc`) | Local Initial Proposal | Comparison & Verdict |
| :--- | :--- | :--- | :--- |
| **Storage Primitive** | `std::vector<std::vector<uint8_t>>` | `std::vector<Chunk>` where `Chunk` owns `std::unique_ptr<uint8_t[]>` | Both guarantee pointer stability. Upstream leverages `std::vector` move semantics; local used explicit immutable heap pointers. |
| **Long-Lived Memory Hygiene** | `reset()` trims chunks if count $> 4$ down to 2 | `reset()` retains all chunks indefinitely | **Upstream is superior.** In long-lived .NET worker threads, unbounded chunk growth during sporadic spikes would permanently inflate process RSS. Upstream bounds retained memory. |
| **Allocation Overhead** | Zero-initializes `std::vector(kChunkSize)` on chunk creation | `std::make_unique<uint8_t[]>` allocates without zero-initialization | Minor performance difference; Libharu overwrites bytes sequentially so zero-initialization has negligible overhead. |
| **Final Adoption** | **Adopted as primary standard** | Replaced | Upstream implementation was adopted cleanly across all environments. |

---

## 4. Test Suite Matrix: Adversarial Tests vs. Continuous Fuzzing

The test infrastructure is structured into two complementary layers:

```
┌────────────────────────────────────────────────────────────────────────┐
│                   Layer 1: Deterministic Adversarial Tests             │
│   (Specific Semantic Attacks, Ghostscript Conformance, Fast PR Gate)   │
├────────────────────────────────────────────────────────────────────────┤
│  • nordiska_adversarial_rendering_tests (Operator injection, 4k txs)   │
│  • nordiska_adversarial_json_fuzz_tests (46 syntax & nesting vectors)  │
│  • nordiska_adversarial_c_api_tests (16-thread stress, ABI limits)     │
│  • nordiska_adversarial_signing_tests (Malformed PDF slot boundaries)  │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │ Complementary Focus
┌───────────────────────────────────▼────────────────────────────────────┐
│                   Layer 2: Continuous Fuzzing & Mutations              │
│   (Combinatorial Matrix, AFL/libFuzzer Drivers, Binary Truncations)    │
├────────────────────────────────────────────────────────────────────────┤
│  • nordiska_fuzz_combinatorial_pipeline (Cross-product engine sweeps)  │
│  • nordiska_fuzz_pdf_signing_slot (15,000 mutations, byte sweeps)      │
│  • nordiska_fuzz_c_api (Permutation sweeps, environment fuzzing)       │
│  • nordiska_fuzz_seed_replay (Deterministic corpus regression replay)  │
└────────────────────────────────────────────────────────────────────────┘
```

### 4.1. Detailed Suite Breakdown

| Suite Name | Target Module | Methodology | Distinct Strength / Value |
| :--- | :--- | :--- | :--- |
| `nordiska_adversarial_rendering_tests` | Rendering Engines (`Native`, `Haru`, `Cairo`) | 0, 100, and 4,000 transactions; PDF operator escape injections (`") Tj /F1 100 Tf ("`, `ET q re f Q BT`, unescaped `\()`, embedded nulls) | **Third-party validation:** Pipes generated byte streams directly into Ghostscript (`gs -dPDFSTOPONERROR -dQUIET -`) to prove syntax validity. |
| `nordiska_adversarial_json_fuzz_tests` | JSON Ingestors (`Simdjson`, `Nlohmann`) | 46 handcrafted payload variations: 200-level array nesting, corrupted UTF-8 (`0xFF`/`0xFE`), missing keys, type confusions | Verifies call-stack safety against recursive parser stack overflow and validates all-or-nothing delivery. |
| `nordiska_adversarial_c_api_tests` | C ABI Interface (`pdf_generator_c_api.cpp`) | Extreme boundaries: null ptrs, 0 length, `SIZE_MAX`, >32 MB safety ceiling, 50,000-char env variables, 16 concurrent threads | Guarantees thread-local error isolation and zero C++ exception leakage across the extern "C" boundary under thread contention. |
| `nordiska_adversarial_signing_tests` | Signature Slot Appender (`signature_slot_appender.cpp`) | Broken PDF structures, missing headers, missing `startxref`, duplicate AcroForm fields, invalid hex strings | End-to-end rendering $\to$ slot append $\to$ digest $\to$ CMS insertion $\to$ Ghostscript check. |
| `nordiska_fuzz_combinatorial_pipeline` | Entire Pipeline Matrix | Multi-dimensional Cartesian product: 2 ingestors $\times$ 3 engines $\times$ 2 compression modes $\times$ 6 signing modes $\times$ 6 capacities | Explores state-space permutations and differential output agreement between Simdjson and Nlohmann across 4,000 mutations. |
| `nordiska_fuzz_pdf_signing_slot` | Binary PDF Parser | Byte-by-byte truncation sweep from 0 to EOF; 15,000 random mutations | Verifies low-level parser robustness against truncated or corrupt byte streams from external signing servers. |
| `nordiska_fuzz_c_api` | C ABI Host Boundary | Systematic parameter permutations, engine environment string casing, callback rejection codes | Verifies that host rejection codes (e.g. status 42) propagate back to thread-local diagnostics. |
| `nordiska_fuzz_seed_replay` | Ingestion & Core | Replays corpus seeds (`account-statement.json`, `annual-tax-report.json`, etc.) | Regression testing for known edge-case inputs. |

---

## 5. Coverage Distribution & Gap Analysis

### 5.1. Do the Suites Complement Each Other or Are They Redundant?
**They are strictly complementary, not redundant:**
1. **Targeted Semantic Attacks vs. Random Bit Mutations:**
   A stochastic fuzzer will almost never generate valid JSON that happens to contain syntactically balanced PDF operator breakout injection strings like `") Tj ET q 0 0 100 100 re f Q BT ("`. The adversarial tests provide exact semantic attack vectors, while the fuzzers explore the high-dimensional combinatorial parameter space.
2. **Third-Party Syntactic Conformance (Ghostscript):**
   Fuzzers assert that code does not crash (`assert` or sanitizers). However, a non-crashing engine can still emit a corrupted PDF that Adobe Reader or PDF printers cannot open. The adversarial suites explicitly execute Ghostscript `-dPDFSTOPONERROR` on generated PDFs, guaranteeing real-world standard compliance.
3. **Execution Speed in CI:**
   The deterministic adversarial tests run in ~3.5 seconds without sanitizer overhead, making them ideal as a mandatory pre-commit / PR gate. The fuzzer suites can run in CI nightlies or long-running AFL/libFuzzer workers.

### 5.2. Coverage Analysis & Potential Blind Spots

While coverage across JSON parsing, PDF generation, and signing is comprehensive, the following areas represent potential edge-case gaps to monitor:

| Pipeline Area | Current Coverage | Potential Blind Spot / Gap | Recommended Mitigation |
| :--- | :--- | :--- | :--- |
| **Document Layout (`layout_builder.cpp`)** | Standard transaction fields, pagination at $y=54.0$, table overflows | Unusually long unbroken text (e.g. 5,000-character description without spaces, oversized customer names) | Add layout tests verifying text truncation or wrapping to prevent off-page clipping. |
| **Container Font Fallbacks (`cairo_pdf_engine.cpp`)** | Standard CP1252 & ASCII via `cairo_toy_font_face_create("Helvetica")` | Minimal Docker containers with zero fonts installed | Add fallback detection if Cairo fails to resolve any font face on a stripped Linux host. |
| **Unicode Normalization (`utf8_to_cp1252.cpp`)** | Swedish characters (`åäöÅÄÖ`), common CP1252 symbols (`€`, `™`, `—`) | Combining diacritics (e.g. letter `e` followed by combining acute `U+0301`) | Verify whether UTF-8 inputs from legacy databases require NFC normalization before CP1252 conversion. |
| **Socket Signer Network Resilience (`pdf_signer.cpp`)** | 10s timeouts, protocol error codes, mock socket server | Signer daemon dropping connection mid-header or stalling on byte 15 of 16 | Add mock tests for abruptly terminated TCP/Unix socket connections causing `EPIPE` / `SIGPIPE`. |
| **P/Invoke GC Marshaling (.NET)** | Tested via C ABI using C++ native callers | .NET GC heap compaction moving unpinned byte arrays during native execution | Verify C# host worker pins memory buffers via `GCHandle.Alloc(..., GCHandleType.Pinned)`. |

---

## 6. Full Test Execution Results

All 11 test suites pass with 100% success across all build configurations:

### 6.1. Complete Test Suite Matrix

| # | Test Suite Target | Type | Release | Debug | ASan / UBSan |
|---|---|---|---|---|---|
| 1 | `nordiska_pdf_signing_tests` | Integration / Unit | **PASSED** (0.04s) | **PASSED** (0.15s) | **PASSED** (0.47s) |
| 2 | `nordiska_json_ingestor_tests` | Unit | **PASSED** (0.02s) | **PASSED** (0.02s) | **PASSED** (0.09s) |
| 3 | `nordiska_pdf_generator_c_api_tests` | Unit / ABI | **PASSED** (0.05s) | **PASSED** (0.06s) | **PASSED** (0.22s) |
| 4 | `nordiska_adversarial_rendering_tests` | Adversarial / Ghostscript | **PASSED** (3.45s) | **PASSED** (3.12s) | **PASSED** (3.93s) |
| 5 | `nordiska_adversarial_json_fuzz_tests` | Adversarial / Security | **PASSED** (0.01s) | **PASSED** (0.03s) | **PASSED** (0.13s) |
| 6 | `nordiska_adversarial_c_api_tests` | Adversarial / Concurrency | **PASSED** (0.12s) | **PASSED** (0.12s) | **PASSED** (0.32s) |
| 7 | `nordiska_adversarial_signing_tests` | Adversarial / Formats | **PASSED** (0.17s) | **PASSED** (0.16s) | **PASSED** (0.19s) |
| 8 | `nordiska_fuzz_seed_replay` | Fuzz / Regression | **PASSED** (0.01s) | **PASSED** (0.01s) | **PASSED** (0.05s) |
| 9 | `nordiska_fuzz_pdf_signing_slot` | Fuzz / Binary Parser | **PASSED** (1.10s) | **PASSED** (1.12s) | **PASSED** (2.64s) |
| 10 | `nordiska_fuzz_combinatorial_pipeline` | Fuzz / Matrix | **PASSED** (2.45s) | **PASSED** (2.53s) | **PASSED** (4.85s) |
| 11 | `nordiska_fuzz_c_api` | Fuzz / ABI Permutations | **PASSED** (0.05s) | **PASSED** (0.05s) | **PASSED** (0.22s) |
| **Total** | **All 11 Test Suites Combined** | | **11 / 11 PASSED** (7.47s) | **11 / 11 PASSED** (7.37s) | **11 / 11 PASSED** (13.12s) |

---

## 7. Code Formatting & Scope Compliance

- **Formatting:** Checked via `./tools/format-native.sh` and `./tools/check-format.sh`. Zero formatting diffs.
- **Repository Cleanliness:** No temporary files, suppression files, or `.pdf` artifacts are tracked or left behind.
- **Scope Compliance:** Strictly adheres to `AGENTS.md`. No modifications to sibling projects (such as `pdf-signer/` or `backend/`).

---

## 8. Merge Recommendation

The `native/pdf_generator` branch is in a hardened, verified, and stable state. All three discovered defects are remediated, test suites cover deterministic adversarial attacks and continuous fuzzing, and all 11 test suites pass cleanly.

**Recommendation:** Proceed with merging `native/pdf_generator` into `main`.
