# Native PDF Generator

## Project boundary

This directory is a standalone native PDF-generation component. Treat it as
its own project, with its own CMake build, tests, dependencies, and
normative target architecture in `docs/target-architecture.md`.

The target architecture is the authority for ownership, dependency direction,
public boundaries, and the intended end state. The README summarizes the
current implementation without overriding that architecture.

The component is intended to be called by another application. The required
integration shapes are:

- one thin native generation executable for process invocation; and
- a C-compatible shared-library boundary for direct .NET invocation.

The reusable application core must remain independent of the caller and must
not depend on .NET, web services, deployment tooling, Docker, or background-job
infrastructure.

## Working rules

- Keep PDF input, validation, rendering, batching, and native API/CLI work in
  scope.
- Follow `docs/target-architecture.md` when deciding where code belongs and
  which dependencies are allowed.
- Keep renderer-specific third-party types inside renderer adapters. Do not
  expose libharu, Cairo, OpenSSL, or other vendor types through public native
  interfaces. Never expose C++ classes, STL types, or exceptions through the
  C ABI.
- Preserve integer minor-unit handling for monetary values.
- Keep executable entry points thin; reusable behavior belongs in the
  application/core targets.
- Run `./tools/format-native.sh` and then `./tools/check-format.sh` before
  every commit that changes native C++ code.
- Add or update focused tests with behavior changes, and run the native CTest
  suite after changes.
- Do not add deployment, Docker, WSL provisioning, .NET integration, or host
  application orchestration tasks to this project unless the project boundary
  is explicitly changed.

## Target shape

The end-state shape is:

```text
nordiska_document_core
nordiska_document_c_api
pdf_generator
```

Input, document-renderer, output, delivery, and PDF-engine adapters remain
separate modules behind the core-owned boundaries. Haru and Cairo are private,
swappable engines beneath the single application-facing PDF renderer.

The separate PDF-signing target, when implemented, remains a native C module
and is not part of the renderer adapter interface.
