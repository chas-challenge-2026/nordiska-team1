# Native PDF Generator

This directory contains the native PDF-generation component for Nordiska v2.
It provides one application core, libharu and Cairo renderer adapters, and
single-report and batch entry points.

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
- **Money:** monetary values are represented as integer minor units, not
  binary floating-point values.
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

Only PDF-rendering dependencies belong in this manifest. The separate
signing program will manage its own cryptographic dependencies.

The manifest should initially live beside this `CMakeLists.txt`. If the PDF
generator and C signer later become one top-level native CMake project, the
manifest can move to `native/`.

The manifest is pinned to a vcpkg baseline, so the dependency versions are
resolved from a known vcpkg commit. Install vcpkg separately (outside this
repository), set `VCPKG_ROOT`, and configure with the vcpkg preset:

```bash
git clone https://github.com/microsoft/vcpkg.git ~/vcpkg
~/vcpkg/bootstrap-vcpkg.sh
export VCPKG_ROOT=~/vcpkg

cmake --preset default
cmake --build --preset default
```

The preset installs the manifest dependencies into the ignored
`build/vcpkg_installed` directory. Cairo is consumed through its pkg-config
metadata because the vcpkg Cairo port does not provide a CMake package config.


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
export VCPKG_ROOT=$HOME/vcpkg
cmake --preset default
cmake --build --preset default
./build/pdf_generator_single sample-input.json
./build/pdf_generator_single sample-input.json --renderer cairo
```

Reports are written to the ignored `output/reports/` directory. The renderer
name and a sequence number are added automatically, for example
`report-cairo-0.pdf` or `report-haru-0.pdf`. Each renderer has its own sequence.

Batch mode takes a directory of report JSON files and writes one PDF per input:

```bash
./build/pdf_generator_batch ./input-reports 4 --renderer haru
```
