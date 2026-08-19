# Native PDF Generator

This directory contains the native PDF-generation component for Nordiska v2.
It currently implements the first single-report slice. Batch generation and
the production PDF backend are still to be implemented.

## Requirements and constraints

These are the requirements inherited from the v2 project documentation.

- **Language:** C or C++; this component currently uses C++23.
- **Output:** generate real, readable PDF files.
- **Purpose:** generate tax reports, including year-end batch reports.
- **Scale target:** 10,000 PDFs containing approximately 500,000 transaction
  records in less than five minutes. [NOTE JJ: Distribute transaction counts
  per PDF according to a log-normal or clamped Pareto distribution rather
  than uniformly.]
- **Integration:** the native component must be callable by the .NET
  background-job system through the integration boundary specified by the
  main project documentation. The executable/process path is the first
  integration target; a C ABI/shared-library path may be added later.
- **Deployment:** the native build must be reproducible in Docker.
- **Money:** monetary values are represented as integer minor units, not
  binary floating-point values.

The v1 implementation used synchronous request-thread work and
`Thread.Sleep`. The native batch worker exists to remove that limitation.
The throughput target is a requirement to measure, not an assumption about
the performance of a particular library.

## Current status

- [x] CMake project and C++23 build.
- [x] JSON input adapter for the temporary report shape.
- [x] `CreatePdf` application use case with basic validation.
- [x] `IPdfRenderer` application-owned renderer boundary.
- [x] Dependency-free proof-of-concept renderer.
- [ ] vcpkg manifest and reproducible third-party dependencies.
- [ ] libharu renderer adapter.
- [ ] Cairo renderer adapter.
- [ ] Single-report production executable.
- [ ] Batch application service and worker queue.
- [ ] Renderer benchmark and backend decision.

The dependency-free renderer is only a development proof of concept. It may
be removed after the real adapters and tests are working; it is not the
planned production backend.

## Working architectural decisions

### One application core, multiple entry points

The reusable native code should be separated from the executable entry points:

```text
nordiska_pdf_application
  ├── CreatePdf
  └── BatchCreatePdf

nordiska_pdf_renderer_haru  -> libharu
nordiska_pdf_renderer_cairo -> Cairo

pdf_generator_single -> application + selected renderer
pdf_generator_batch  -> application + selected renderer
```

The batch queue and worker management belong in a batch application service.
`main_batch.cpp` should only parse arguments, construct dependencies, and
start that service.

### Renderer boundary

The application talks to `IPdfRenderer`, never directly to a PDF library:

```cpp
class IPdfRenderer {
public:
    virtual ~IPdfRenderer() = default;
    virtual void render(const Report&, const std::filesystem::path&) = 0;
};
```

Third-party types must not appear in `Report`, `CreatePdf`, or
`IPdfRenderer`. Each backend is an adapter implementing this interface.

The initial backends to implement and benchmark are libharu and Cairo. The
project documents mention libpoppler in one place as an alternative; it is
not part of the current implementation plan unless that requirement is
explicitly changed.

### Dependency management

Use vcpkg in manifest mode for the native dependencies. The initial
dependencies are:

- `libharu`
- `cairo`
- `openssl` for the separate signing module

The manifest should initially live beside this `CMakeLists.txt`. If the PDF
generator and C signer later become one top-level native CMake project, the
manifest can move to `native/`.

### Signing

PDF signing is a separate native module, not part of the renderer. It is
planned as a C module using OpenSSL/libcrypto. The C++ PDF generator should
produce the PDF first; signing should consume the generated artifact through
its own C API or executable boundary.

## First-slice usage

The current proof of concept demonstrates:

```text
sample-input.json -> pdf_generator -> report.pdf
```

Example input:

```json
{
  "account_number": "SE1234567890",
  "transactions": [
    {
      "date": "2026-01-05",
      "type": "deposit",
      "currency": "SEK",
      "amount_minor": 100000
    }
  ]
}
```

`amount_minor` is an integer to avoid binary floating-point money errors.
`currency` is currently required to be a three-letter uppercase ISO 4217
code. Currency-specific decimal scales and additional validation remain
future work.

Build from this directory:

```bash
cmake -S . -B build
cmake --build build --config Release
./build/pdf_generator sample-input.json report.pdf
```

On a multi-configuration generator, the executable may instead be under a
`Release/` subdirectory.

## Current structure

```text
src/main.cpp                          current CLI composition root
src/application/create_pdf.cpp        single-report orchestration/validation
src/adapters/json_report_reader.cpp   temporary JSON input adapter
src/adapters/minimal_pdf_renderer.cpp proof-of-concept renderer
include/nordiska/report.hpp            domain data only
include/nordiska/pdf_renderer.hpp     PDF port/interface
```

Planned additions include `libharu_pdf_renderer.cpp`,
`cairo_pdf_renderer.cpp`, `batch_create_pdf.cpp`, and a separate
`native/pdf_signer/` module.

## Open decisions to preserve

- [ ] Select the production renderer after benchmarking both adapters.
- [ ] Decide whether batch failures are fail-fast or collected and reported
  after the batch.
- [ ] Establish the renderer's thread-safety model before sharing renderer
  instances between workers.
- [ ] Decide whether the optional .NET-facing native library is needed after
  the process executable path is working.
- [ ] Define PDF metadata, fonts, pagination, Unicode, and signing-placement
  requirements before treating a renderer benchmark as complete.
