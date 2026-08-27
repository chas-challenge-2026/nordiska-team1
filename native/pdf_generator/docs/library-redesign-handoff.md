# Library-first redesign handoff

## Checkpoint intent

This commit intentionally removes the native generation CLI entry points
(`pdf_generator_single`, `pdf_generator_batch`, and `run_pdf_gen.sh`). Do not
reintroduce a CLI until the native library's input, output, result, and error
contracts are agreed and verified.

The target architecture still permits a thin executable delivery adapter in a
future milestone. It must be rebuilt as a script-like consumer of the same
library composition; it must not own parsing, renderer selection, validation,
batching, output publication, or error policy.

## Current working library path

```text
nordiska_document_generate_json (C ABI)
  -> JsonInputAdapter::import_text
  -> GenerateDocuments
  -> PdfRenderer (shared presentation and pagination)
  -> private PDF engine (Haru by default; Cairo available for comparison)
  -> CallbackOutputDestination
  -> caller callback receives one completed PDF
```

The C ABI is deliberately small: one UTF-8 JSON report object in, one complete
PDF callback out, with a status code and bounded UTF-8 error buffer. The C API
does not expose C++ types, choose a renderer, or own an output implementation.

## What remains before rebuilding a CLI

1. Stabilize and test the JSON report contract, including the actual tax-report
   data and presentation requirements.
2. Extend the JSON adapter from one report to a normalized one-or-many source.
3. Add renderer tests for pagination, text encoding, tables, and readable-PDF
   validation; retain Haru as the production default unless benchmark evidence
   changes that decision.
4. Add any caller-visible configuration only when a concrete second use case
   requires it.
5. Rebuild one thin CLI only after those library contracts are stable.

## Verification at this checkpoint

Run from `native/pdf_generator`:

```bash
cmake --preset default
cmake --build --preset default
ctest --test-dir build --output-on-failure
```

The native suite has five tests, including C ABI valid-PDF, invalid-input, and
callback-failure coverage.
