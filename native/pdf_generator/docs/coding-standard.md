# Native C++ coding standard

This project uses C++23 and `clang-format` 18 or newer. The checked-in
`.clang-format` file is the authoritative source for whitespace, indentation,
line wrapping, braces, include ordering, and comment reflow.

## Required conventions

- Use four spaces for indentation. Tabs are forbidden.
- Keep lines at 100 columns or fewer unless a generated string or an external
  API makes that impractical.
- Use attached braces for every control-flow body and do not put short
  control-flow statements on one line.
- Put the directly related project header first in an implementation file,
  followed by other project headers and then standard or third-party headers.
- Use `PascalCase` for types, `snake_case` for functions and local variables,
  and a trailing underscore for private data members.
- Prefer RAII, standard-library types, explicit ownership, and `const` or
  `noexcept` where they express the API contract.
- Keep executable entry points thin. Reusable behavior belongs in the native
  application or adapter libraries.
- Public native interfaces must not expose renderer-library or other vendor
  types. The C ABI must not expose C++ classes, STL types, exceptions, or
  other C++ implementation details.
- Keep comments focused on intent, invariants, ownership, or non-obvious
  constraints. Do not use comments to restate obvious code.
- Every behavior change requires focused tests and a passing native CTest run.

The standard applies to hand-written C++ under `include/`, `src/`, and
`tests/`. Vendored dependencies, generated benchmark data, PDFs, and the
separate Python or shell tools are not reformatted by the C++ formatter.

## Formatting commands

From `native/pdf_generator`:

```bash
./tools/format-native.sh
./tools/check-format.sh
cmake --build --preset default
ctest --test-dir build --output-on-failure
```

`check-format.sh` is suitable for a pre-commit hook or CI job and fails if any
tracked native C++ file differs from the checked-in `clang-format` style.
