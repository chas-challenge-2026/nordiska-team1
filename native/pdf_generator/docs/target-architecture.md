# Native Document Generator target architecture

## Status and authority

This document defines the normative target architecture for the Nordiska
native document generator. It describes the intended end product, ownership
boundaries, dependency direction, runtime model, and extension points.

The words **must**, **must not**, **should**, and **may** are architectural
requirements. If another native-project document conflicts with this one,
this document takes precedence for architectural decisions. Implementation
plans may describe the current state or a migration sequence, but they do not
redefine the target architecture.

This is deliberately not an implementation plan. It does not prescribe task
order, migration commits, or temporary compatibility arrangements.

## Product definition

The product is a reusable native document-generation system. Its first
supported workflow is report data to PDF, but its architecture must not bind
the application core to JSON, PDF, files, a particular PDF library, .NET, or
a command-line process.

The same native core must support both delivery modes:

1. a shared library callable from .NET through a stable C ABI; and
2. a thin executable callable by .NET or another host as a process.

Both delivery modes must execute the same application use case and obey the
same validation, rendering, result, and failure semantics.

The product has one caller-facing operation: generate documents from the
provided reports. One report produces one document. Several reports produce
several documents. Callers must not choose between separate single and batch
APIs. Concurrency and batching are internal execution policies.

## Architectural invariants

The following rules define the architecture:

- The domain and application core must remain independent of input formats,
  document formats, output destinations, delivery mechanisms, and third-party
  rendering libraries.
- The core must own the interfaces through which external behavior is used.
  Concrete adapters implement those interfaces and depend inward on the core.
- One-report and many-report requests must use the same generation path.
- Input format, document format, output destination, and delivery mechanism
  are separate dimensions and must not be collapsed into one abstraction.
- The core must not select JSON, PDF, Haru, Cairo, files, callbacks, CLI, or
  .NET directly.
- Only a composition root may select and connect concrete adapters.
- Third-party types must remain inside their concrete adapter or engine.
- No C++ types, exceptions, ownership conventions, or standard-library
  containers may cross the C ABI.
- A document renderer must produce document bytes without deciding where
  those bytes are ultimately stored or delivered.
- An output adapter must deliver bytes without deciding how report content is
  rendered.
- Adding an input format, document format, output destination, PDF engine, or
  delivery mechanism must not require changes to domain or application logic.

## Conceptual model

Four concepts must remain distinct:

### Input source

An input source supplies one or more normalized domain reports. It hides the
origin and representation of the data.

Examples include JSON in a file, JSON in memory, CSV, a database query, or a
network source. Input adapters own syntax parsing, structural validation, and
mapping into domain values. They do not own document rendering or output.

### Document renderer

A document renderer converts one validated report into one document format.
PDF, HTML, and plain text are document formats.

The initial implementation provides only a PDF document renderer. The generic
renderer boundary must nevertheless remain independent of PDF so another
format can be added without changing the application core.

### Output destination

An output destination receives each generated document and determines where
its bytes go. A file, memory buffer, .NET callback, HTTP response, and database
blob are output destinations.

Output destination is not the same as document format. PDF-to-file,
PDF-to-memory, HTML-to-file, and HTML-to-callback are valid independent
combinations.

### Delivery adapter

A delivery adapter exposes the product to an external caller. The shared C
ABI and the console executable are delivery adapters. They translate external
requests into core-owned concepts, select concrete adapters through the
composition root, invoke the application core, and translate results back to
the caller.

## Target hierarchy

```text
Native Document Generator
│
├── Domain
│   ├── Report
│   ├── Transaction
│   ├── value types
│   └── business validation
│
├── Application Core
│   └── GenerateDocuments
│       ├── consumes one or many reports
│       ├── validates every report
│       ├── coordinates rendering and publication
│       ├── preserves report identity and result order
│       └── owns concurrency and failure policy
│
├── Core-owned Ports
│   ├── report input/source port
│   ├── document renderer port
│   ├── output destination/factory port
│   └── per-document byte sink port
│
├── Input Adapters
│   ├── JSON file input
│   ├── JSON memory input
│   └── future CSV/database/network inputs
│
├── Document Renderers
│   ├── PdfRenderer
│   │   └── internal PDF engine strategy
│   │       ├── LibHaru engine
│   │       └── Cairo engine
│   ├── future HTML renderer
│   └── future text renderer
│
├── Output Adapters
│   ├── file output
│   ├── memory output
│   ├── .NET callback output
│   └── future HTTP/database outputs
│
├── Delivery Adapters
│   ├── C ABI shared library
│   └── console executable
│
├── Composition
│   └── selects and connects concrete implementations
│
└── Diagnostics
    └── benchmarks and verification tools
```

## Dependency direction

Dependencies must point inward toward policy and domain concepts:

```text
Delivery Adapters
        │
        ▼
   Composition
        ├──────────────────────────────► Application Core
        │                                      │
        ├──► Input Adapters                    ▼
        ├──► Document Renderers ◄────── Core-owned Ports
        └──► Output Adapters                   │
                │                              ▼
                └──────────────────────────► Domain
```

The diagram expresses dependency direction, not runtime data flow. At runtime,
data flows from an input adapter through the application service and renderer
to an output adapter.

The allowed dependencies are:

| Layer | May depend on | Must not depend on |
| --- | --- | --- |
| Domain | Standard language facilities needed by domain values | Adapters, rendering libraries, filesystem policy, CLI, .NET |
| Application | Domain and core-owned ports | Concrete adapters, Haru, Cairo, JSON libraries, CLI, C ABI |
| Core-owned ports | Domain and boundary-safe core types | Concrete implementations and third-party libraries |
| Input adapters | Input libraries, domain, and input ports | Renderers, output adapters, delivery adapters |
| Document renderers | Renderer ports, domain presentation, and private format dependencies | Input adapters, delivery adapters, output policy |
| Output adapters | Output ports and destination-specific dependencies | Input parsing and report presentation policy |
| Delivery adapters | Boundary translation and shared composition/application facades | Domain business policy and vendor-specific rendering code |
| Composition | All modules required to assemble one application | Reusable business behavior |
| Diagnostics | Public or dedicated diagnostic seams | Production policy ownership |

## Domain ownership

The domain defines the canonical meaning of a report independently of how it
arrives or how it is rendered. Monetary values must remain integer minor
units. Domain types must not contain JSON nodes, database records, Haru or
Cairo handles, output paths, callbacks, command-line options, or .NET types.

Business validation belongs to the domain or a domain validation service.
Examples include required account identity, required transactions, currency
rules, and other report invariants. Every input path must reach the same
business validation. An input adapter may reject malformed syntax or incorrect
source types, but it must not become the sole owner of business rules.

The application core must not assume that reports came through the JSON
adapter. Programmatically constructed reports and future input adapters must
receive identical validation.

## Application ownership

The application core owns one use case: `GenerateDocuments`.

That use case must:

- consume reports from a core-owned source abstraction or an equivalent
  normalized report sequence;
- treat a single report as a collection of one;
- validate each report through the shared business rules;
- render exactly one document for each valid report;
- obtain a distinct output destination for each document;
- preserve stable report identity regardless of parallel execution;
- return a result for every submitted report; and
- hide worker counts, queues, renderer instances, and scheduling from callers.

The application core owns orchestration, not representation or transport. It
must not parse JSON, scan directories, generate filenames, construct Haru or
Cairo objects, print console messages, or invoke .NET callbacks.

Concurrency is an internal execution policy. A one-report request may execute
without a worker pool. A many-report request may use bounded parallelism.
Observable result identity and ordering must not depend on scheduling.

Independent report failures should not prevent unrelated valid reports from
being attempted. The result model must identify per-report success or failure.
A request-level failure is reserved for conditions that prevent the request
from being interpreted or execution from starting safely.

## Core-owned ports

Ports define what the application needs, not how a library happens to provide
it. Exact C++ declarations may evolve, but their responsibilities must remain
separate.

### Report source port

The report source port yields one or more canonical `Report` values. It must
support normalization of source-specific cardinality into a single sequence.
The application must not branch into separate single and batch workflows.

The source contract should permit a streaming implementation so large inputs
do not require every report to be retained simultaneously. An initial adapter
may materialize a collection as long as that choice does not leak into the
application contract.

### Document renderer port

The document renderer port converts one validated `Report` into one document
and writes its bytes to a per-document byte sink. It must describe a generic
document renderer rather than a PDF-library renderer.

The core may know renderer-independent metadata such as document media type or
recommended extension when needed for publication. It must not know engine
names such as Haru or Cairo.

### Output destination port

The output destination port creates or supplies a destination for each
generated document. It receives stable document metadata, such as request
index and report identity, and returns a per-document byte sink or equivalent
publication handle.

Filename selection, directory layout, memory ownership, callback invocation,
and transport-specific publication belong to output adapters. They do not
belong to the application core.

### Byte sink port

The byte sink is the renderer-to-destination streaming boundary for one
document. It accepts byte chunks and supports successful completion. An
unfinished or failed sink must not publish a partial document as a successful
result.

File, memory, discard, callback, and future streaming sinks may implement this
contract. The contract must not contain PDF-specific operations.

## Input adapter rules

An input adapter owns three source-facing responsibilities:

1. obtaining source data;
2. validating source syntax and structure; and
3. mapping source values into canonical domain reports.

These responsibilities may be split between a source reader and parser when
useful, but they must remain outside domain and application logic.

The initial JSON adapters must support both one report object and a collection
of report objects. Both shapes must normalize to the same report sequence.
The distinction must not survive beyond the input adapter.

Adding another input format must require only a new input adapter and its
composition registration. It must not require changes to `GenerateDocuments`,
document renderers, output adapters, or delivery adapters beyond selecting the
new adapter.

## Document renderer rules

Each document format has one application-facing renderer implementation.
Therefore PDF has one `PdfRenderer` that implements the generic document
renderer port.

Future `HtmlRenderer` or `TextRenderer` implementations would be siblings of
`PdfRenderer`. They would not be PDF engines and would not depend on the PDF
subsystem.

A document renderer owns presentation decisions for its format, including
content arrangement, formatting, layout, and pagination where applicable. It
must not own final destination policy.

## PDF renderer subsystem

The PDF subsystem has a facade-and-engine hierarchy:

```text
generic document renderer port
            │
            ▼
       PdfRenderer
            │
            ▼
   private PDF engine port
       ├── LibHaru engine
       └── Cairo engine
```

`PdfRenderer` is the only PDF renderer visible to the application. It owns
shared PDF report presentation, including money display, report text,
document structure, layout, and pagination. Those rules must not be duplicated
inside Haru and Cairo integrations.

The private PDF engine port represents the rendering primitives or neutral PDF
document model required by `PdfRenderer`. It is an implementation detail of
the PDF module, not a core-owned application port.

Haru and Cairo implementations must:

- translate the private PDF model or drawing operations into vendor calls;
- own vendor resource lifetime and error translation;
- write generated bytes through the supplied byte sink; and
- keep all vendor headers and types inside the engine implementation.

Haru and Cairo must not independently define report formatting, business
validation, output naming, or caller-facing behavior.

Production composition must select one benchmark-approved default PDF engine.
The alternative engine may remain available for benchmarks, compatibility,
and replacement testing. Engine selection must not appear in the application
core, normal caller contract, or report model.

## Output adapter rules

An output adapter owns destination-specific behavior. Examples include:

- atomic file creation and publication;
- in-memory ownership and transfer;
- delivery of completed documents to a .NET callback;
- writing to a network stream; and
- storing a document as a database blob.

Adding an output destination must require only a new output adapter and its
composition registration. It must not require changes to input adapters,
business rules, application orchestration, or document renderers.

The output adapter determines destination-specific names and paths. The core
provides stable identity and document metadata but must not scan directories,
derive sequence numbers from existing filenames, or depend on the current
working directory.

## Delivery adapters and build products

The reusable C++ core is linked into two caller-facing build products. The
build system may use static or object libraries internally, but there must be
one authoritative core implementation.

### Shared library

The shared library exposes a stable C ABI suitable for .NET P/Invoke. The C
ABI is a delivery adapter, not the domain or application model.

The ABI must use C-compatible values such as fixed-width integers, byte
buffers with explicit lengths, UTF-8 strings with explicit ownership, opaque
handles where necessary, callbacks, status codes, and caller-provided error
storage.

The ABI must catch and translate every C++ exception before returning. It must
define callback lifetime, thread-affinity, ownership, cancellation, and error
semantics. No C++ class, standard-library type, exception, allocator contract,
or filesystem object may cross the ABI.

The .NET callback output adapter may deliver one completed document at a time
without requiring all generated documents to remain in native memory.

### Console executable

The executable is a thin process delivery adapter. It may parse command-line
arguments, read files or standard input, choose process-appropriate adapters,
write diagnostics, and translate application results into exit status and a
machine-readable result manifest.

There is one generation executable and one generation command. Callers do not
choose a single or batch executable. Input cardinality determines output
cardinality automatically.

The executable must not reimplement validation, report generation, rendering,
concurrency, or failure policy. Those behaviors belong to the shared core.

### Behavioral parity

Given equivalent normalized reports, renderer configuration, and output
destination behavior, library and process delivery must produce equivalent
documents and per-report results. Delivery-specific transport details may
differ; application semantics must not.

## Composition ownership

The composition root is the only place allowed to know the complete concrete
object graph. It selects:

- the input adapter appropriate to the external request;
- the document renderer;
- the production PDF engine when PDF is selected;
- the output adapter;
- execution configuration; and
- the application service.

Renderer names, format identifiers, CLI flags, environment configuration, and
deployment defaults are translated here. Selection logic must not be copied
across entry points. The shared library and executable may have separate
boundary translation, but they must reuse shared composition facilities where
their concrete configuration is equivalent.

## Runtime flow

The canonical runtime flow is:

```text
external request
    │
    ▼
delivery adapter
    │
    ▼
composition selects adapters
    │
    ▼
input adapter yields normalized reports
    │
    ▼
GenerateDocuments validates and coordinates each report
    │
    ├──► document renderer ──► per-document byte sink
    │                                  │
    │                                  ▼
    └────────────────────────── output adapter publishes result
                                       │
                                       ▼
                              per-report success or failure
```

For JSON, a single report object and an array of report objects enter the same
flow after normalization. For process delivery, the output adapter normally
publishes files. For library delivery, the output adapter normally publishes
through a callback or caller-owned destination.

## Result and identity model

Every submitted report receives a stable request-local identity before
parallel work begins. At minimum, this identity includes its input position;
domain identifiers may supplement it but must not replace it unless uniqueness
is guaranteed by the domain contract.

Every report produces one result containing either publication metadata or a
structured failure. Result association with the input report must remain
stable regardless of execution order.

Human-readable exception strings are not the cross-boundary result model.
Delivery adapters must translate structured application failures into stable
status values plus optional diagnostic text appropriate to their boundary.

## Source placement rules

The intended source hierarchy communicates ownership. Exact filenames may
vary, but code must be placed according to these boundaries:

```text
include/nordiska/
├── domain/                 public domain values and rules
├── application/            public application facade and result types
├── ports/                  core-owned extension interfaces
└── adapters/               vendor-neutral adapter construction APIs

src/
├── domain/                 domain behavior and validation
├── application/            GenerateDocuments orchestration
├── adapters/
│   ├── input/
│   │   └── json/           JSON readers and parsers
│   ├── renderers/
│   │   └── pdf/
│   │       ├── pdf_renderer.cpp
│   │       └── engines/
│   │           ├── libharu/
│   │           └── cairo/
│   └── output/             file, memory, callback, and other destinations
├── delivery/
│   ├── c_api/              shared-library boundary translation
│   └── cli/                process boundary translation
├── composition/            concrete object graph and selection
└── diagnostics/            benchmark-only orchestration and reporting

tests/
├── domain/
├── application/
├── contracts/              reusable port and adapter contract tests
├── adapters/
├── delivery/
└── integration/
```

Public headers must expose only contracts intentionally available to other
native modules. Adapter construction APIs may expose concrete adapter names or
vendor-neutral configuration, but never vendor types. Vendor-specific engine
headers and private PDF model types must remain under the PDF implementation
and must not be installed as public application headers.

## Placement decision guide

When adding code, determine its owner with these questions:

1. Does it define the meaning or validity of report data? It belongs to the
   domain.
2. Does it coordinate the generation use case across abstract capabilities?
   It belongs to the application layer.
3. Does it state a capability the application requires from external code?
   The interface belongs to core-owned ports.
4. Does it parse or acquire a particular input representation? It belongs to
   an input adapter.
5. Does it decide how a document format presents a report? It belongs to that
   document renderer.
6. Does it translate neutral PDF operations into Haru, Cairo, or another PDF
   library? It belongs to a private PDF engine.
7. Does it decide where generated bytes are stored or sent? It belongs to an
   output adapter.
8. Does it translate an external calling convention into the application
   contract? It belongs to a delivery adapter.
9. Does it choose concrete implementations and connect them? It belongs to
   composition.
10. Does it exist only to measure or inspect the system? It belongs to
    diagnostics and must not own production behavior.

Code with several answers has several responsibilities and must be split at
the corresponding boundaries.

## Extension rules

The following changes define whether the architecture is genuinely modular:

| Desired extension | Required change | Core changes allowed |
| --- | --- | --- |
| Add CSV input | Implement and register a CSV input adapter | No |
| Read JSON from memory instead of a file | Implement or configure a JSON memory source | No |
| Add HTML documents | Implement and register an HTML document renderer | No |
| Add plain-text documents | Implement and register a text document renderer | No |
| Replace Haru with another PDF library | Implement a private PDF engine | No |
| Change the production PDF engine | Change composition or build configuration | No |
| Save documents to a database | Implement and register an output adapter | No |
| Return documents to .NET | Implement the C ABI callback output adapter | No |
| Add another host protocol | Add a delivery adapter | No |

If one of these extensions requires editing domain or application behavior,
the existing boundary is misplaced or incomplete.

## Testing boundaries

Tests must reinforce the architecture rather than bypass it:

- domain tests verify business invariants without adapters;
- application tests use fake ports and verify one/many parity, stable identity,
  per-report results, and orchestration;
- contract tests verify every implementation of a shared port obeys the same
  lifecycle and failure semantics;
- PDF renderer tests verify shared presentation independently of engine choice;
- PDF engine tests verify vendor translation and valid PDF output;
- delivery tests verify C ABI safety and CLI translation; and
- integration tests verify equivalent behavior through library and process
  delivery.

Benchmarks may choose specific engines and destinations to compare them, but
benchmark-only selection must not leak into normal caller contracts.

## Explicit non-goals

This architecture does not require the following now:

- implementing HTML, text, CSV, database, HTTP, or other future adapters;
- exposing a generic plugin system to external users;
- allowing normal callers to select Haru or Cairo;
- moving .NET business or orchestration logic into the native core;
- exposing the internal C++ application API as a stable cross-language ABI;
- combining PDF signing with document rendering; or
- preserving separate single and batch products.

Only JSON input, PDF rendering, and the currently required output destinations
need concrete production implementations initially. The extension boundaries
must nevertheless follow this document from the start.

## End-state acceptance rules

The architecture is achieved only when all of the following are true:

- one application use case handles both one and many reports;
- a shared native core powers both the C ABI library and console executable;
- input representations are replaceable through input adapters;
- document formats are replaceable through document renderers;
- output destinations are replaceable through output adapters;
- `PdfRenderer` is the sole application-facing PDF renderer;
- PDF engines are private and swappable without core changes;
- production engine selection is confined to composition;
- vendor types do not leak out of their engines;
- the C ABI contains only C-compatible contracts and translates all failures;
- process and library delivery have equivalent application behavior;
- concurrency is invisible to callers and does not alter result identity;
- no entry point duplicates business validation or generation behavior; and
- every source file has one architectural owner under the placement rules.
