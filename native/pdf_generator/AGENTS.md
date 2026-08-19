# Native PDF Generator

## Project boundary

This directory is a standalone native PDF-generation component. Treat it as
its own project, with its own CMake build, tests, dependencies, and
implementation plan in `docs/nordiska-native-implementation.md`.

The component is intended to be called by another application. The supported
integration shapes are:

- the native executable (`pdf_generator_single` or `pdf_generator_batch`), as
  the primary caller-facing path;
- a small C-compatible shared-library boundary, if direct in-process calls
  are later needed.

The reusable application core must remain independent of the caller and must
not depend on .NET, web services, deployment tooling, Docker, or background-job
infrastructure.

## Working rules

- Keep PDF input, validation, rendering, batching, and native API/CLI work in
  scope.
- Keep renderer-specific third-party types inside renderer adapters. Do not
  expose libharu, Cairo, OpenSSL, C++ classes, STL containers, or exceptions
  through public application interfaces or a C ABI.
- Preserve integer minor-unit handling for monetary values.
- Keep executable entry points thin; reusable behavior belongs in the
  application/core targets.
- Add or update focused tests with behavior changes, and run the native CTest
  suite after changes.
- Do not add deployment, Docker, WSL provisioning, .NET integration, or host
  application orchestration tasks to this project unless the project boundary
  is explicitly changed.

## Main targets

The expected target shape is:

```text
nordiska_pdf_application
nordiska_pdf_renderer_haru
nordiska_pdf_renderer_cairo
pdf_generator_single
pdf_generator_batch
```

The separate PDF-signing target, when implemented, remains a native C module
and is not part of the renderer adapter interface.
