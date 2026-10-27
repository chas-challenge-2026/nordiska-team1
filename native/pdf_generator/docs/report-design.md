# Branded report layout

Issue: #283

The account statement follows the supplied private statement's visual language:
A4 paper, a small wordmark, navy title band, amber punctuation, compact tables,
and a centered bank footer. The annual tax report uses the same components;
its composition is inferred because no annual reference was available.

## Boundaries and ownership

```text
JSON -> PdfRenderingJob -> LayoutBuilder -> DocumentLayout -> PdfEngine -> signing
                              |
                       report::Composer
                         /          \
                 ReportStyle      prepared logo
```

- **Domain and ingestion** retain the existing customer/document schema.
  `LayoutContext` borrows the batch's customer name during layout; generated
  text owns its strings. Each recipient receives the explicitly requested dummy
  address (`Gatnamnet 32`, `123 45 Staden`, `SE`) until real address data is part
  of the input contract. Current interest rates are omitted when unavailable.
- **ReportStyle** contains presentation values and a version identifier. The
  current version is compiled with the generator, so a deployment pins the
  style and prepared asset together. It is not a dynamic theme registry.
- **Brand resources** own immutable decoded RGB pixels. The wordmark is stored
  as precompressed RGB bytes without source metadata; its source alpha mask is
  composited onto white paper. Function-local static initialization prepares
  the pixels once per process, safely across concurrent calls. Stateless C API
  construction does not decode the asset for every generator or batch.
- **LayoutBuilder** selects a concrete report composition. `report::Composer`
  owns one document's mutable page and table flow. It shares page furniture,
  wrapping, right alignment, repeated headings, row continuation, and final
  page numbering. It contains no renderer handles or signing logic.
- **Text metrics and encoding** use the same WinAnsi normalization and standard
  Helvetica advances for wrapping and placement. The compatibility encoding
  include under `rendering/` remains available. The reference uses HK Nova;
  we do not extract or redistribute its embedded font subsets. Helvetica is
  the current substitute. Cairo resolves the existing system
  Helvetica/Arial/sans-serif fallback, so exact glyph shapes depend on installed
  fonts; Haru and Native specify PDF Core-14 Helvetica.
- **DocumentLayout** contains ordered value commands: text, lines, filled
  rectangles, and RGB images. Coordinates start at the page's top left; text
  coordinates specify baselines. Painting order is explicit. Image commands
  borrow immutable `RasterImage` storage, which must outlive rendering; the
  default brand resource has process lifetime. The C ABI does not expose these
  C++ types. C++ consumers of the former `texts`/`lines` vectors must migrate to
  `commands`.
- **Renderers** translate commands without knowing document types or branding.
  Each PDF owns its backend resource handles. Native and Haru deduplicate image
  objects across pages; Cairo prepares one local image surface per resource per
  render. Native can embed the prepared compressed pixels directly.
- **Signing and delivery** run after rendering through their existing contracts.
  The C ABI, all-or-nothing delivery, and signed-document hashing are unchanged.

The reusable core has no host startup, .NET, service, or deployment dependency.
There is no new inheritance hierarchy, global mutable theme, or template engine.
If a second brand becomes a real requirement, pass an immutable style/resource
bundle explicitly at the layout boundary instead of introducing per-job asset
loading or backend-specific branding.

## Typography

The shared style defines semantic text roles: 22 pt bold title, 12 pt bold band
subtitle, 13 pt bold section heading, 11 pt regular body/address, 11 pt bold table
heading, and 6 pt footer. Field labels use bold with regular values on the same
baseline; the bank name in the footer is bold. Address baselines are 14.84 pt
apart. Both report compositions use these shared roles.

## Pagination and validation

Tables reserve the footer area and repeat headings on continuation pages.
Ordinary rows stay together; oversized rows continue at line boundaries without
truncating cells. All fields, including long unbroken tokens and custom titles,
flow within measured widths. Custom titles flow below the standard document
heading so arbitrary input cannot overflow the title band.

`nordiska_report_layout_tests` checks Swedish text, normalization, long fields,
page bounds, lossless row continuation, repeated branding and headers, image
reuse, invalid resources, and rendering through all three engines. The existing
signing, C API, adversarial, and fuzz replay suites exercise the full pipeline.

For a local visual preview with synthetic data:

```sh
./build/debug/pdf_generator -i docs/golden_customer_batch_sample.json \
  -o /tmp/nordiska-pdf-preview --renderer haru
```

The private reference PDF remains outside tracked files. Never use its customer
content in fixtures, screenshots, issue attachments, or committed previews.

## Local performance comparison

Release-mode smoke measurement of the initial branded implementation (`078528a`,
before the typography adjustments) against the pre-change branch head, on the same
machine: five rounds of 300 warmed, unsigned V1 C API calls, using the golden
synthetic batch (one statement and one tax report). Times include both PDFs,
configuration/component construction, hashing, and callback delivery.

| Engine | Before, ms/batch | Branded, ms/batch | Before, bytes/document | Branded, bytes/document |
| --- | ---: | ---: | --- | --- |
| Native | 0.143 | 0.180 | 1,216 / 1,224 | 4,948 / 4,880 |
| Haru | 0.205 | 0.943 | 3,351 / 3,354 | 7,406 / 7,336 |
| Cairo | 1.183 | 2.154 | 22,187 / 22,861 | 27,762 / 27,161 |

These are local measurements, not production throughput guarantees. The design
adds artwork, colored page furniture, more text, and table rules. Haru loads
prepared RGB through its public API and recompresses image streams per output
PDF; Cairo also does document-local image/font serialization. Only Native embeds
the precompressed image stream directly. Shared decoding avoids repeated asset
preparation but does not eliminate backend serialization costs. The Haru default
is retained; any default-engine change needs its own compatibility decision.
