# Native document generator implementation status

The normative end state is defined by
[`target-architecture.md`](target-architecture.md). This document records the
current implementation checkpoint.

## Current module graph

```text
nordiska_document_core
  ├── domain report values and validation
  └── GenerateDocuments application service

nordiska_document_input_json   -> nlohmann/json
nordiska_document_output       -> file, memory, null, callback destinations
nordiska_document_renderer_pdf -> PdfRenderer
                                  ├── private LibHaru engine
                                  └── private Cairo engine
nordiska_document_composition  -> default PdfRenderer composition
nordiska_document_c_api       -> JSON input + composition + callback output
```

`GenerateDocuments` handles one and many reports through the same path. It
validates reports, creates one renderer per worker, opens a destination for
each document, and returns one stable result per input index. The application
does not know about JSON, PDF engines, filesystem paths, or callbacks.

## Completed

- C++23 is enabled for every native target.
- Domain values and validation are independent of rendering.
- Generic `IDocumentRenderer`, `IByteSink`, and `IOutputDestination` ports are
  separated from their adapters.
- PDF presentation and pagination are centralized in `PdfRenderer`.
- Haru and Cairo are private swappable PDF engines.
- File, memory, null, and callback output destinations are separate adapters.
- The C ABI uses the same application and composition path as native callers.
- The native CTest suite covers application results, JSON input, output sinks,
  benchmark metrics, and C ABI valid/invalid/callback-failure behavior.

## Remaining work

- Normalize one JSON object and a JSON collection through a report-source port.
- Expand the report model and presentation to the actual tax-report contract.
- Add focused PDF tests for pagination, Unicode, tables, and readable-PDF
  validation for both engines.
- Add a thin executable delivery adapter only after the library contracts are
  stable.
- Keep PDF signing as a separate native C module.
