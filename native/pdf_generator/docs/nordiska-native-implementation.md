# Nordiska Native Implementation Plan

## Scope

This plan covers only the native C/C++ modules. The .NET integration is defined elsewhere and is not changed here.

## Agreed architecture

Use one reusable native core with multiple thin entry points:

```text
nordiska_pdf_application
  ├── CreatePdf
  └── BatchCreatePdf

nordiska_pdf_renderer_haru  -> libharu
nordiska_pdf_renderer_cairo -> Cairo

pdf_generator_single -> application + one selected renderer
pdf_generator_batch  -> application + one selected renderer

pdf_signer            -> C implementation + OpenSSL/libcrypto
```

`IPdfRenderer` remains the application-owned boundary. Third-party library types must not appear in `Report`, `CreatePdf`, or `IPdfRenderer`.

The current `MinimalPdfRenderer` is temporary and may be deleted after the real adapters and tests are working. Tests should use a fake renderer rather than retaining the minimal production backend.

## Phase 1 — Install dependency management in WSL

- [ ] Install and bootstrap vcpkg once inside WSL.
- [ ] Set `VCPKG_ROOT` in the WSL shell environment.
- [x] Create the manifest beside the current CMake project: `native/pdf_generator/vcpkg.json`.
- [x] Declare the initial dependencies:
  - `libharu`
  - `cairo`
  - `openssl`
- [x] Create `vcpkg-configuration.json` with a reproducible registry baseline.
- [x] Add CMake configure presets using the vcpkg toolchain file.
- [x] Add generated directories such as `build/` and `vcpkg_installed/` to `.gitignore`.
- [ ] Verify that a clean WSL configure/build works.

The package manager is a build-time tool. It does not become part of the application architecture and does not require a Microsoft account or hosted service.

## Phase 2 — Refactor native CMake targets

- [x] Separate application/core sources from renderer adapter sources.
- [x] Create independent targets for the Haru and Cairo adapters.
- [x] Link each third-party dependency only to the adapter that uses it.
- [x] Add `pdf_generator_single` as the one-report executable.
- [x] Add `pdf_generator_batch` as the batch executable.
- [x] Keep `main` files thin: argument parsing, dependency construction, and invocation only.
- [x] Do not add a shared library until the executable path is working.

Expected target shape:

```text
nordiska_pdf_application
nordiska_pdf_renderer_haru
nordiska_pdf_renderer_cairo
pdf_generator_single
pdf_generator_batch
```

## Phase 3 — Implement PDF renderer adapters

- [x] Implement `LibHaruPdfRenderer`.
- [x] Implement `CairoPdfRenderer`.
- [x] Keep all libharu headers/includes inside the Haru adapter implementation.
- [x] Keep all Cairo headers/includes inside the Cairo adapter implementation.
- [x] Make both adapters produce the same output contract through `IPdfRenderer`.
- [x] Ensure output is written atomically or through a temporary file before replacement.
- [ ] Add renderer-level tests for valid output, empty/invalid reports, Unicode/text handling, and file errors.

## Phase 4 — Single and batch execution

- [x] Keep `CreatePdf` responsible for one report.
- [x] Implement `BatchCreatePdf` as an application service rather than placing the queue/thread-pool logic directly in `main_batch.cpp`.
- [ ] Add a bounded worker queue and configurable worker count.
- [x] Define failure behavior for one failed report: fail-fast versus collect-and-report.
- [x] Ensure each worker owns its renderer state if the selected backend is not thread-safe.
- [x] Add batch error reporting and deterministic exit codes.
- [ ] Benchmark one process handling a batch rather than starting one process per PDF.

## Phase 5 — C PDF signing module

- [ ] Create a separate `native/pdf_signer` C project/target.
- [ ] Keep signing separate from PDF rendering.
- [ ] Use OpenSSL `libcrypto` and the high-level EVP signing APIs.
- [ ] Expose a small C API or CLI; do not expose C++ classes or STL types.
- [ ] Return explicit error codes and write bounded error messages to a caller-provided buffer.
- [ ] Add verification tests using the public key.
- [ ] Document key handling, algorithm parameters, and signature placement before implementation.

## Phase 6 — Benchmark and choose the renderer

- [ ] Create a fixed corpus of representative reports.
- [ ] Benchmark Haru and Cairo using the same input data and output requirements.
- [ ] Measure wall-clock time, throughput, peak memory, output size, and failure rate.
- [ ] Validate that every output is a real readable PDF, not only that generation succeeded.
- [ ] Compare required features: fonts, Unicode, pagination, metadata, tables, and signing compatibility.
- [ ] Select the production backend based on measured results and document the decision.

## Phase 7 — Optional native shared-library boundary

Only after the executable path is stable:

- [ ] Add a thin `extern "C"` wrapper if direct native-library invocation is required.
- [ ] Use C-compatible arguments only: byte buffers, lengths, UTF-8 strings, paths, status codes, and caller-owned error buffers.
- [ ] Never expose `std::string`, `std::vector`, exceptions, C++ classes, or `std::filesystem::path` across the ABI.
- [ ] Build the wrapper as `.so` for Linux and `.dll` only for Windows builds.
- [ ] Keep the process executable as the primary integration path unless direct calls are demonstrably necessary.

## Definition of done

- [ ] A clean WSL checkout can install dependencies and build with CMake.
- [ ] A clean Docker build can reproduce the native build.
- [ ] Single-report generation works through the selected renderer.
- [ ] Batch generation reuses the same application core.
- [ ] Haru and Cairo can be benchmarked without changing application code.
- [ ] Signing is an independent C module using OpenSSL.
- [ ] No third-party PDF or crypto types leak through application interfaces.
- [ ] Tests and benchmark results are checked into the project documentation.
