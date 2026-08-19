# First PDF generator slice

This is the smallest end-to-end native component:

```text
sample-input.json -> pdf_generator -> report.pdf
```

Build and run with CMake:

```powershell
cmake -S . -B build
cmake --build build --config Release
.\build\Release\pdf_generator.exe sample-input.json report.pdf
```

The temporary input shape is:

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
`currency` is a three-letter ISO 4217 code. Currency-specific decimal scales
and validation will be expanded later.

## Structure

```text
src/main.cpp                         CLI composition root
src/application/create_pdf.cpp       use-case orchestration and validation
src/adapters/json_report_reader.cpp  temporary JSON input adapter
src/adapters/minimal_pdf_renderer.cpp PDF backend adapter
include/nordiska/report.hpp           domain data only
include/nordiska/pdf_renderer.hpp     PDF port/interface
```

The application talks to `IPdfRenderer`, never directly to a PDF library. A
future backend can implement the same interface:

```text
MinimalPdfRenderer      current dependency-free proof of concept
LibHaruPdfRenderer      future libharu adapter
CairoPdfRenderer        possible future cairo adapter
```

Changing the backend should only require changing the composition in
`src/main.cpp` and adding the adapter's implementation/dependency.

The public seam to preserve is:

```cpp
class IPdfRenderer {
public:
    virtual void render(const Report&, const std::filesystem::path&) = 0;
};
```

This uses normal C++ headers and translation units. C++23 is the language
standard; C++20/23 named modules are a separate feature and are not needed for
this first small project.
