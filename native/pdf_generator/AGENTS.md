# Native PDF Generator

## Project boundary

This directory is a standalone native PDF-generation component. Treat it as
its own project, with its own CMake build, tests and dependencies.

The component is intended to be called by another application as a C cmpatible library on windows or linux.
The reusable application core must remain independent of the caller and must
not depend on .NET, web services, deployment tooling, Docker, or background-job
infrastructure.

## Working rules

- Never expose C++ classes, STL types, or exceptions through the
  C ABI.
- Run `./tools/format-native.sh` and then `./tools/check-format.sh` before
  every commit that changes native C++ code.
- Add or update focused tests with behavior changes, and run the native CTest
  suite after changes.
- Do not add deployment, Docker, WSL provisioning, .NET integration, or host
  application orchestration tasks to this project unless the project boundary
  is explicitly changed.

## Modern C++ architecture
Reason architecture-first, not file-first. Before making changes,
identify:

- the build and target graph;
- subsystem and module boundaries;
- the runtime ownership and object graph;
- control and data flow; and
- public/stable boundaries versus private implementation.

Trace code top-down:

```text
system → target → subsystem → type → function → statement
```

Use idiomatic modern C++, drawing on C++20/23/26 features supported by the
target's configured language standard and toolchain. Prefer:

- value semantics, RAII, and the Rule of Zero;
- explicit ownership, with `std::unique_ptr` for unique dynamic ownership and
  raw pointers or references for non-owning access;
- `std::span` and `std::string_view` for borrowed views;
- `std::optional`, `std::variant`, and `std::expected` for explicit state and
  result modeling;
- strong types, `enum class`, and standard containers;
- lambdas, algorithms, and ranges where they improve intent;
- templates constrained with concepts;
- `constexpr` where it naturally expresses compile-time invariants; and
- composition over inheritance, with runtime polymorphism only when variation
  is genuinely runtime.

Avoid by default:

- manual `new`/`delete`, owning raw pointers, and unnecessary `shared_ptr`;
- C-style casts and sentinel values for state or errors;
- macro-based abstractions where language features suffice;
- inheritance hierarchies without a real subtype or runtime-polymorphism
  requirement; and
- factories, interfaces, dependency-injection layers, or generic abstractions
  without a concrete variation, ownership, testing, or boundary problem.

Treat language constructs as architectural signals:

- ownership types express lifetime;
- views express borrowing;
- result types express failure contracts;
- variants express valid alternatives;
- interfaces express runtime substitution; and
- templates and concepts express compile-time substitution.

At C, OS, FFI, wire-format, or ABI boundaries, use the representation required
by that boundary, then translate immediately into idiomatic internal C++
types. Do not let boundary-oriented C representations dictate the internal
C++ architecture. This guidance does not change the separate PDF-signing
module's native C requirement.

For each new abstraction, be able to answer:

1. What responsibility does it own?
2. What invariant or design decision does it protect?
3. Who owns its lifetime?
4. What depends on its public interface?
5. What future change does this boundary isolate?

Prefer the simplest design that makes ownership, lifetime, state, and
dependencies explicit

 Apply these principles in all your code edits and design desicions or advice. 
