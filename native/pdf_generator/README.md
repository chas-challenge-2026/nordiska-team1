# Nordiska Native Document Generator

This directory contains the standalone C++23 document-generation library. Its
first document format is PDF, with Haru as the production engine and Cairo as
a comparison engine. The .NET portal and deployment projects are outside this
component's scope.

The normative architecture is defined in
[`docs/target-architecture.md`](docs/target-architecture.md).

## Structure

```text
domain/       canonical reports and business validation
application/  GenerateDocuments orchestration and per-document results
ports/        generic renderer, output, and byte-sink contracts
adapters/
  input/      JSON parsing
  output/     file, memory, null, and callback destinations
  renderers/  PDF facade and private Haru/Cairo engines
composition/  production object graph and default engine choice
delivery/     external calling conventions such as the C ABI
```

The application depends on `IDocumentRenderer`, not on PDF libraries. The PDF
facade owns report presentation and pagination; the private engines translate
that neutral PDF model into Haru or Cairo calls. Output destinations own file,
memory, and callback behavior.

## How it works

```text
caller
  -> input adapter              parses JSON into a canonical Report
  -> GenerateDocuments          validates and coordinates one or many reports
  -> IDocumentRenderer          generic document-format boundary
  -> PdfRenderer                builds shared PDF layout and pagination
  -> PDF engine                 translates the layout to Haru or Cairo
  -> IByteSink                  streams completed document bytes
  -> output destination          writes a file, stores memory, or invokes a callback
  -> per-document result
```

The application layer does not know whether input came from JSON, whether the
document is PDF, which PDF engine is used, or where the bytes are delivered.
Those choices are connected in composition. A single report follows the same
path as a batch containing one report.

## Build and test

From this directory, with vcpkg available through `VCPKG_ROOT`:

```bash
cmake --preset default
cmake --build --preset default
ctest --test-dir build --output-on-failure
```

The reusable build products are `nordiska_document_core`, the input/output and
PDF adapter libraries, `nordiska_document_composition`, and the
`nordiska_document_c_api` shared library. The C ABI currently accepts one JSON
report and synchronously returns one complete PDF through its callback.

## Benchmark

The benchmark measures JSON loading, memory rendering, null rendering, file
persistence, and end-to-end generation. It currently selects the Haru engine:

```bash
./build/pdf_generator_benchmark <input-directory> --iterations 3
```

Money remains represented as integer minor units. Renderer selection belongs
to composition and diagnostics, not to the application contract.
