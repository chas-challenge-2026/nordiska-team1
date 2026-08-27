# Nordiska Native Implementation Plan

> [!IMPORTANT]
> This file records an earlier/current implementation plan. The normative end
> state is defined by `target-architecture.md`, which takes precedence for
> architecture, module ownership, and dependency decisions.

## Scope

This plan covers the standalone native PDF-generator component in this
directory. It owns PDF report input, rendering, batching, validation, and the
native API/CLI boundary used by a caller. It does not cover deployment,
Docker, WSL provisioning, .NET integration, background-job orchestration, or
host application wiring.

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

`IPdfRenderer` remains the application-owned PDF boundary. Renderers write
bytes to the generic `IByteSink` boundary; `FileByteSink`, `MemoryByteSink`,
and `NullByteSink` own persistence or discard behavior. Third-party library
types must not appear in `Report`, `CreatePdf`, `IPdfRenderer`, or
`IByteSink`.

The output flow is:

```text
Report -> IPdfRenderer -> IByteSink
```

The CLI preserves its existing behavior by using `FileByteSink`, including
atomic publication. A benchmark can separately measure input loading,
rendering to memory/null, file persistence, and full end-to-end generation.

The current `MinimalPdfRenderer` is temporary and may be deleted after the real adapters and tests are working. Tests should use a fake renderer rather than retaining the minimal production backend.

## Phase 1 — Native dependency setup

- [ ] Document the one-time local vcpkg prerequisite for developers.
- [ ] Verify that a clean local configure/build works when `VCPKG_ROOT` is set.
- [x] Create the manifest beside the current CMake project: `native/pdf_generator/vcpkg.json`.
- [x] Declare the initial dependencies:
  - `libharu`
  - `cairo`
  - `openssl`
- [x] Create `vcpkg-configuration.json` with a reproducible registry baseline.
- [x] Add CMake configure presets using the vcpkg toolchain file.
- [x] Add generated directories such as `build/` and `vcpkg_installed/` to `.gitignore`.
The package manager is a build-time tool. It does not become part of the
application architecture or the caller-facing boundary.

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
- [x] Separate PDF rendering from output persistence with `IByteSink`.
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
- [x] Add a separate Haru benchmark harness using the deterministic synthetic
  input corpus, with input, memory-render, null-render, persistence, and
  end-to-end phase accounting.
- [ ] Measure wall-clock time, throughput, peak memory, output size, and failure rate.
- [ ] Validate that every output is a real readable PDF, not only that generation succeeded.
- [ ] Compare required features: fonts, Unicode, pagination, metadata, tables, and signing compatibility.
- [ ] Select the production backend based on measured results and document the decision.

## Phase 7 — Native library boundary

Only after the executable path is stable:

- [x] Add a thin `extern "C"` wrapper for direct .NET invocation.
- [x] Use C-compatible arguments only: byte buffers, lengths, UTF-8 strings, callbacks, status codes, and caller-owned error buffers.
- [x] Never expose `std::string`, `std::vector`, exceptions, C++ classes, or `std::filesystem::path` across the ABI.
- [x] Build the wrapper as `.so` for Linux and `.dll` only for Windows builds.
- [x] Add C ABI tests for real PDF output, validation errors, and callback rejection.

## Definition of done

- [ ] A clean local checkout can configure and build with CMake.
- [ ] Single-report generation works through the selected renderer.
- [ ] Batch generation reuses the same application core.
- [ ] Haru and Cairo can be benchmarked without changing application code.
- [ ] Signing is an independent C module using OpenSSL.
- [ ] No third-party PDF or crypto types leak through application interfaces.
- [ ] The executable path is usable by a caller, and the optional C ABI is
      added only if direct library calls are required.
- [ ] Tests and benchmark results are checked into the project documentation.
