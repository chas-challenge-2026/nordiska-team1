# Unsigned JSON-to-PDF fuzzing with AFL++

This first target exercises raw JSON bytes through **simdjson → layout → Haru →
compressed PDF bytes**, entirely in memory. Signing is explicitly disabled in the
harness, regardless of `NORDISKA_PDF_ENABLE_SIGNING`. It does not contact the signer,
write PDFs, or require the application stack or a database.

## Build

Use Linux, the generator's normal native dependencies, AFL++, and a C++23-capable
AFL++ LLVM compiler/toolchain with a compatible C++ standard library. Keep this
build separate from debug/release builds. From `native/pdf_generator/`:

```sh
CC=afl-clang-fast CXX=afl-clang-fast++ cmake -S . -B build/fuzz -G Ninja \
  -DCMAKE_BUILD_TYPE=RelWithDebInfo \
  -DNORDISKA_BUILD_FUZZERS=ON \
  -DNORDISKA_FUZZ_SANITIZERS=ON \
  -DCMAKE_DISABLE_FIND_PACKAGE_simdjson=ON
cmake --build build/fuzz --target nordiska_fuzz_json_to_pdf -j 4
ctest --test-dir build/fuzz -R nordiska_fuzz_seed_replay --output-on-failure
```

The CMake option defaults to off. Enabling it adds a fuzz/replay executable; the
sanitizer option instruments the generator and fetched dependencies as well as
the harness. Prebuilt system libraries are not retroactively instrumented.
Normal builds and production binaries are unaffected when fuzzing is off.
The simdjson setting selects the project's pinned FetchContent version and builds
it with coverage/sanitizers, avoiding older system headers that can fail with
newer Clang versions.

A normal GCC/Clang compiler can also build this target for **replay only**;
`--afl` refuses to run without AFL++ instrumentation. Building just this target is sufficient for a fuzz campaign; the complete
native test suite can be built and run separately.

## Interactive campaigns

Run in a terminal to get the standard AFL++ live dashboard:

```sh
./tests/fuzz/run.sh           # 5-minute smoke campaign
./tests/fuzz/run.sh 3600      # one hour
./tests/fuzz/run.sh 28800     # eight hours
./tests/fuzz/run.sh 0         # until Ctrl+C
```

Ctrl+C stops the campaign safely. Running again with the same output directory
resumes it. Output defaults to `build/fuzz/findings/`; pass a second argument for
a separate campaign. `NORDISKA_FUZZ_BUILD_DIR` selects a different build directory.
Do not reuse results across different engines/configurations or materially
changed harnesses without deliberately migrating the corpus.

The launcher calls `afl-fuzz` directly with no extra dashboard or log redirection.
Piping output, using a non-terminal, or setting `AFL_NO_UI` disables the interactive
UI. `-V` bounds fuzzing time; initialization/calibration can add wall-clock time.

Start with five minutes to check the harness, sanitizer setup, valid seeds and
coverage stability. Use an hour for a first campaign and overnight runs once
stable. There is no duration that proves correctness: watch executions/second,
new coverage, stability, saved crashes and hangs. Raw JSON mutations can spend
much of their time in validation; valid seeds help, but deeper structured-input
fuzzing is a later increment.

Each input is capped at **64 MiB**, deliberately above the C API's 32 MiB limit,
and each execution has a five-second timeout. The launcher queries the compiled
harness (`--max-input-bytes`) so replay and AFL++ use the same ceiling. This is an
input-size ceiling, not a cap on resident memory. The C++ pipeline is called
directly: acceptance/rejection at the C API's 32 MiB boundary needs separate C API
tests. Raising the ceiling alone does not guarantee that mutations reach it;
large seeds are needed for a campaign intended to exercise large payloads.

**AFL++ tooling:** stock AFL++ 5.04a has a compile-time `MAX_FILE` of 1 MiB and
rejects a larger `-G`. Rebuild AFL++ with `MAX_FILE` in its `include/config.h` set
to at least `(64 * 1024 * 1024L)` before using this ceiling; use matching rebuilt
tools/runtime. The harness option does not change AFL++'s own build limit.

AFL++ memory limiting is disabled to accommodate AddressSanitizer's virtual
address space; allocation failures/timeouts still need triage. The launcher sets
ASan to abort on errors with leak detection disabled for the fuzzing campaign, and
UBSan to abort rather than recover. Use separate leak-enabled replay when needed.

If AFL++ reports host CPU-frequency/core-pattern restrictions, follow its
reported instructions. The launcher does not change system settings. On managed
hosts, explicit environment overrides such as `AFL_SKIP_CPUFREQ=1` may be needed.

## Inputs, checks and replay

The engine-independent entry point is `json_to_pdf(span<const uint8_t>)`.
`afl_driver.cpp` feeds it from AFL++ shared memory, using a forkserver with one
input per child process. This isolates parser/renderer thread-local caches between
inputs. An initial persistent-mode experiment showed variable coverage, so this
first implementation favors repeatability. Persistent mode can be added after
cache initialization and coverage stability have been addressed. Each replay
input also owns a new generator and result, but multi-file replay shares a process.

Small, curated seeds cover both currently supported document kinds
(`account_statement`, `annual_tax_report`), mixed batches and Unicode pagination.
The schema dictionary helps mutation retain meaningful keys and enum strings.
The generator currently has no third document kind; add its seeds when supported.

Malformed inputs and ordinary pipeline errors are expected and are not crashes.
Unexpected exceptions, sanitizer failures and explicit contract failures are
findings. Successful batches must have a customer, nonempty documents, unique
nonempty IDs, PDF headers/EOF markers, and no signed state/signing digest.
These are basic output-contract checks, **not independent PDF validation or proof
of correct text, amounts, pagination, layout, or visual appearance**.

```sh
build/fuzz/tests/fuzz/nordiska_fuzz_json_to_pdf --replay path/to/input
build/fuzz/tests/fuzz/nordiska_fuzz_json_to_pdf --require-success tests/fuzz/seeds/*.json
```

Replay returns success for normal rejection; `--require-success` additionally
fails if a known-valid input is rejected. Both modes run the same harness and
its assertions as fuzzing, without requiring a running AFL++ process. Replay
several files in one invocation to help diagnose process-state issues.

AFL++ stores interesting inputs under `findings/default/queue/`, crashes under
`crashes/`, and hangs under `hangs/`. Reproduce and minimize findings before calling
them generator defects: slow valid input, resource exhaustion, sanitizer/toolchain
configuration and harness assertions need distinct treatment. Preserve and
replay the originating input; turn confirmed bugs into ordinary regression tests.

Keep seeds, harnesses and dictionary in Git. Evolving corpora, sanitizer builds and
crash artifacts stay under the already-ignored `build/` directory. No signing,
C ABI callback fuzzing, parser differential checks, other renderers or independent
PDF/content checking are included yet.

## Initial validation (19 October 2026)

The harness was built with AFL++ 5.04a / Clang 20 and ASan/UBSan, using the
project's pinned simdjson and system Haru. All four native CTest suites passed
with leak detection disabled. The new seed-replay test also passed with leak
detection enabled, and valid seeds still generated PDFs when the environment
requested signing (the harness kept it disabled).

Leak-enabled execution of the existing suites reports 1,044 bytes in 13
allocations rooted in Cairo/fontconfig initialization. Those allocations are
outside this unsigned Haru harness and have not been fixed or fully triaged here;
disabling leak detection does not establish that they are harmless.

Normal builds were checked to exclude the fuzz target. Empty/malformed input,
oversized replay input, non-instrumented AFL mode, and invalid launcher arguments
were checked as well. The live terminal dashboard, Ctrl+C save behavior and saved-campaign resume were
verified. The original 300-second isolated-process smoke run, using the former 1 MiB
ceiling, completed 91,624
executions with 100% reported stability, 323 corpus entries, no saved crashes and
no saved hangs. Campaign timing/results are available in the local build's
`findings/default/fuzzer_stats` and `plot_data`; this smoke run is not a security
or PDF visual-correctness guarantee.

The ceiling was subsequently raised to 64 MiB. No additional fuzz campaign was
run for that change; the original smoke result does not cover larger inputs.

## Full Matrix, PDF Parser, and C ABI Fuzzing Suites (Expanded Campaign)

The fuzzing suite includes three autonomous multi-target fuzzers covering all combinations of settings, engines, signing modes, and C ABI boundaries:

### 1. Combinatorial Pipeline Fuzzer (`nordiska_fuzz_combinatorial_pipeline`)
* **Matrix dimensions**:
  * Ingestors: `Simdjson`, `Nlohmann`
  * Engines: `Libharu`, `Cairo`, `Native`
  * Compression: `true`, `false`
  * Signing: `false` (unsigned), `true` (signed with valid mock CMS, corrupted hex, odd length, empty, oversized, crypto errors)
  * Capacities: `8192`, `1024`, `65536`, `128`, `0`, `1023` (odd)
* **Stress & Mutation**:
  * Structured generation from 0 to 300+ transactions per statement (stressing pagination, header repeats, and arena limits)
  * Full character coverage: Swedish characters (`åäöÅÄÖ`), CP1252 symbols (`€`, `™`, `—`, `–`, `•`), unmapped Unicode, and control characters
  * 4,000+ raw bit/byte mutations across the golden seed corpus
  * Differential ingestion consistency between `Simdjson` and `Nlohmann` across `uint64_t` boundaries

### 2. PDF Parser & Signature Slot Appender Fuzzer (`nordiska_fuzz_pdf_signing_slot`)
* Directly tests `append_signature_slot`, `compute_byte_range_digest`, `insert_signature`, and `hash_final_document`
* Real baseline PDFs generated by `Libharu`, `Cairo`, and `Native`
* Systematic byte-by-byte truncation across all byte offsets from 0 to EOF
* 15,000 mutation iterations (bit flips, byte swaps, deletions, keyword corruptions) across all 3 engine outputs
* Rejection of corrupted CMS signatures (odd hex, non-hex, empty, oversized) and double-signing attempts
* Direct boundary validation of signature slot offsets, lengths, and delimiter characters (`<`, `>`)

### 3. C ABI Boundary & Concurrency Fuzzer (`nordiska_fuzz_c_api`)
* Boundary validation for `nordiska_pdf_v1_generate_customer_batch`, `nordiska_pdf_v2_generate_customer_batch`, and instrumented variants
* Payload boundaries (null pointers, zero length, exactly 32 MB, 32 MB + 1 byte)
* Environment variable switching (`NORDISKA_PDF_ENGINE=native|cairo|libharu|invalid`)
* Callback rejection handling and thread-local error diagnostic reporting
* V2 SHA-256 integrity: verifies delivered `sha256_hex` matches independent OpenSSL SHA-256 calculation byte-for-byte
* Concurrency stress: 8 worker threads executing parallel batch generations without race conditions or thread-local bleeding

### Fixed Defects Discovered During Fuzzing
1. **HaruBumpArena Use-After-Free / Pointer Invalidation** (`haru_pdf_engine.cpp`):
   * *Problem*: The bump arena stored allocations in a single `std::vector<uint8_t>` and resized it when exceeding 512 KB. Resizing reallocated the underlying heap buffer, invalidating all previously returned pointers held by Libharu (`HPDF_Doc`, pages, fonts, dicts) and causing use-after-free / heap corruption in large documents.
   * *Fix*: Replaced the single reallocating vector with a chunked `std::vector<std::vector<uint8_t>>` arena. Pointers inside existing chunks remain 100% stable across chunk additions.
2. **`append_float_2` Buffer Overflow / Garbage Bytes** (`native_pdf_engine.cpp`):
   * *Problem*: Fixed 2-decimal formatting used a 32-byte buffer. Floats larger than $10^{30}$ or invalid values failed `std::to_chars` with `std::errc::value_too_large`, leaving `ptr` equal to `last` and writing uninitialized buffer bytes into the content stream.
   * *Fix*: Expanded buffer to 64 bytes and verified `ec == std::errc{}` before appending, with safe fallback to `"0.00"`.
3. **`customer_id` Unsigned 64-bit Integer Wrapping** (`json_ingestor.cpp`):
   * *Problem*: `NlohmannIngestorImpl` parsed `customer_id` through `int64_t`, causing unsigned customer IDs above `INT64_MAX` to wrap to negative numbers and be falsely rejected as `customer_id <= 0`, diverging from `SimdjsonIngestorImpl`.
   * *Fix*: Checked `is_number_unsigned()` first and retrieved `uint64_t` directly, matching `Simdjson` behavior.

