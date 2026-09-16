# Nordiska Tax & Interest Reporting Pipeline
## Conceptual Design Document

**Project:** Chas Extended Challenge 2026 — Nordiska  
**Status:** Conceptual design  
**Scope:** Interest reporting, tax-report preparation, rendering, provenance, and signing  
**Primary design goal:** Build a scalable reporting system that is not tied to Sweden, SEK, PDF, or one tax authority.

---

## 1. Background

Nordiska wants to scale customer self-service without scaling manual customer-service work at the same rate.

The inherited version generates a tax-related document inline during the request flow. The project brief identifies tax-report generation as a performance-sensitive area and specifically calls for native C/C++ modules for:

- batch generation of tax-report PDFs,
- hashing and signing of generated PDFs,
- audit metadata and traceability.

The design should therefore separate financial facts from presentation formats and from jurisdiction-specific reporting rules.

The system should not fundamentally be a "PDF generator."

It should be a small reporting pipeline where PDF is one possible output.

---

## 2. Core Design Principle

The stable core of the system answers one question:

> For a given account and reporting period, how much interest was paid or made available, expressed in the account's native currency?

Everything else is a transformation or output step.

Conceptually:

```text
BANK / LEDGER DATA
        |
        v
INTEREST SUMMARY
        |
        v
OPTIONAL TRANSFORMATIONS
        |
        +--> currency conversion
        +--> jurisdiction-specific rules
        +--> other enrichment
        |
        v
CANONICAL REPORT DATA
        |
        +--> PDF
        +--> TXT
        +--> CSV
        +--> JSON
        +--> regulatory XML
        |
        v
HASH / SIGN / AUDIT
```

---

## 3. Design Goals

### 3.1 Currency-neutral

The core must support accounts denominated in currencies such as:

- SEK
- NOK
- EUR

without special-casing Sweden or Swedish kronor.

The design should also remain extensible to other ISO 4217 currencies.

### 3.2 Jurisdiction-neutral

Swedish tax reporting is one adapter.

The core reporting model must not contain assumptions such as:

- Swedish tax rates,
- Skatteverket,
- KU20,
- SEK conversion,
- Swedish rounding rules.

Those belong in jurisdiction-specific processing stages.

### 3.3 Format-neutral

PDF is a delivery format, not the underlying report.

The same report facts should be able to produce:

- PDF,
- plain text,
- CSV,
- JSON,
- XML,
- future formats.

### 3.4 Deterministic

The same canonical input and the same renderer version should produce reproducible report content.

This is important for:

- auditability,
- hashing,
- signing,
- debugging,
- regeneration.

### 3.5 Batch-friendly

Year-end reporting may involve every customer.

The system should therefore support:

- pre-generated reports,
- parallel workers,
- bounded memory use,
- idempotent generation,
- cheap regeneration where possible.

### 3.6 Auditable

The system should make it possible to answer:

- Which financial facts produced this report?
- Which conversion rate was used?
- Which rules were applied?
- Which renderer version produced the artifact?
- Has the artifact changed?
- Was the artifact signed by Nordiska?

---

# 4. Domain Boundary

## 4.1 Interest is the primary financial fact

For this subsystem, the important financial fact is interest paid or made available during a period.

A conceptual interest summary contains:

```text
account
customer
period
currency
interest paid
```

Example:

```text
Account:        A123
Customer:       C42
Period:         2026-01-01 to 2027-01-01
Currency:       EUR
Interest paid:  183742 minor units
```

For EUR, this represents:

```text
1,837.42 EUR
```

The reporting subsystem does not need to treat every normal account transaction as report input unless a particular report requires transaction-level detail.

---

# 5. Money Representation

## 5.1 Money is stored as integer minor units

Actual monetary values should not use binary floating point.

Conceptually:

```text
Money
    currency
    minor_units
```

Examples:

```text
EUR 12345
SEK 12345
NOK 12345
JPY 12345
```

The meaning of `minor_units` is determined by the currency definition.

For example:

```text
EUR 12345 -> 123.45 EUR
JPY 12345 -> 12,345 JPY
```

## 5.2 Currency scale comes from currency metadata

The system should maintain one currency registry based on ISO 4217 metadata.

Conceptually:

```text
EUR -> 2 decimal minor-unit digits
SEK -> 2
NOK -> 2
JPY -> 0
BHD -> 3
```

The scale should therefore not be duplicated inside every stored money value.

## 5.3 Higher-precision calculation values are separate

Internal calculations may require precision smaller than the official currency minor unit.

Examples include:

- accrued daily interest,
- exchange-rate calculations,
- intermediate tax calculations.

Those values should use a fixed-decimal calculation representation with explicit scale.

Conceptually:

```text
Money
    = booked/payable monetary amount
    = official currency minor units

DecimalAmount
    = calculation value
    = explicit precision/scale
```

Rounding into `Money` should occur according to the relevant banking or reporting rule.

---

# 6. Interest Events

The system should distinguish between:

```text
interest accrued
```

and:

```text
interest paid / credited / made available
```

The reporting subsystem primarily cares about the latter.

A bank may calculate interest every day while only making it available:

- monthly,
- quarterly,
- annually,
- at account closure,
- according to another product rule.

The event that becomes relevant for reporting is therefore not necessarily every internal accrual calculation.

Conceptually:

```text
daily accrual
      |
      v
internal calculation
      |
      v
interest becomes payable / available
      |
      v
InterestCredit event
```

---

# 7. Interest Aggregation

The first processing stage converts ledger/accounting data into a compact period summary.

Conceptually:

```text
ledger
   |
   v
interest events for account + period
   |
   v
sum in native currency
   |
   v
AccountInterestSummary
```

A conceptual summary contains:

```text
account_id
customer_id
period_start
period_end
currency
interest_paid_minor_units
```

This is the first stable interface in the reporting pipeline.

It contains no Swedish tax logic.

---

# 8. Running Aggregates

For scalability, the bank may maintain derived running totals.

Example:

```text
customer/account/year
currency
interest paid so far
```

This can make year-end reporting nearly constant-time per account.

However:

> The running aggregate should not replace the authoritative ledger.

The ledger remains the source of truth.

The aggregate is a derived optimization.

Conceptually:

```text
AUTHORITATIVE LEDGER
        |
        +--> interest events
        |
        v
DERIVED PERIOD TOTAL
        |
        v
REPORTING PIPELINE
```

The bank should retain the ability to recompute and verify the aggregate from the ledger.

---

# 9. Currency Conversion

Currency conversion is an optional pipeline stage.

It does not belong in the core interest aggregator.

Example:

```text
1,837.42 EUR interest
        |
        v
FX conversion
        |
        v
converted reporting value
```

If no conversion is needed:

```text
native currency
      |
      +--> pass through
```

If conversion is required:

```text
native currency
      |
      v
FX adapter
      |
      v
reporting currency
```

---

# 10. FX Provenance

Whenever a currency conversion becomes relevant to accounting or reporting, the system should preserve the conversion inputs.

Conceptually:

```text
original amount
original currency
conversion date
exchange rate
exchange-rate source
converted amount
```

The important design principle is:

> Do not rely on reconstructing a historical conversion later if the value was already established at the time of the event.

If a reporting-relevant FX rate is fixed when interest becomes available, that rate should be attached to or referenced by the relevant financial event.

This improves:

- reproducibility,
- auditability,
- historical regeneration,
- independence from future API changes.

---

# 11. Jurisdiction Adapters

Country-specific tax and regulatory behavior comes after the generic financial data.

Conceptually:

```text
AccountInterestSummary
        |
        v
optional FX conversion
        |
        v
Jurisdiction Adapter
        |
        v
Canonical Report Data
```

Examples of future adapters:

```text
Swedish reporting adapter
Norwegian reporting adapter
other jurisdiction adapter
```

A jurisdiction adapter may define:

- required fields,
- target/reporting currency,
- tax calculations,
- rounding rules,
- reporting period rules,
- regulatory identifiers,
- regulator-specific output requirements.

The generic interest aggregation layer should not know about any of these.

---

# 12. Swedish Reporting as One Adapter

For the current project, Sweden is the initial jurisdiction.

The Swedish adapter may eventually handle concepts such as:

- Swedish tax-reporting fields,
- Skatteverket-specific requirements,
- KU20-related data,
- conversion into SEK where required,
- Swedish regulatory rounding rules.

These are implementation concerns of the Swedish adapter, not properties of the generic core.

This distinction is important because Nordiska operates in a Nordic/European context and wants technology that can scale with new products and markets.

---

# 13. Canonical Report Data

After all required financial and jurisdiction-specific transformations are complete, the system creates canonical report data.

This represents:

> The exact facts that the bank intends to communicate for this report.

Conceptually it may contain:

```text
customer
account
reporting period

native currency
native interest amount

optional:
reporting currency
converted interest amount
tax values
jurisdiction
report type
regulatory identifiers
```

This is the key boundary between:

```text
financial/reporting logic
```

and:

```text
presentation
```

The PDF is not the report.

The canonical data is the report.

---

# 14. Output Rendering

Renderers consume canonical report data.

Conceptually:

```text
                    Canonical Report
                          |
          +---------------+---------------+
          |               |               |
          v               v               v
         PDF             TXT             JSON
          |
          +--> customer-facing artifact
```

Additional renderers may include:

```text
CSV
JSONL
XML
regulator-specific XML
future formats
```

A renderer should not recalculate financial facts.

Its responsibility is presentation and serialization.

---

# 15. Customer Data Export

The broader self-service system may also expose richer account data for customer-controlled analysis.

This is a separate product from the annual tax report.

Conceptually:

```text
ACCOUNT DATA
     |
     +--> transaction export
     |       |
     |       +--> CSV
     |       +--> JSON
     |       +--> JSONL
     |
     +--> annual interest report
             |
             +--> PDF
             +--> TXT
             +--> JSON
             +--> regulatory output
```

A transaction-level export can support use cases such as:

- spreadsheet analysis,
- budgeting tools,
- personal software,
- local LLM analysis,
- customer-owned financial analysis.

Examples:

- "How much did I spend on groceries this year?"
- "Which subscriptions increased?"
- "Compare this year's spending with last year."

This does not mean the tax-report pipeline itself should ingest or output every transaction.

The two features should share underlying account data but remain separate interfaces.

---

# 16. Provenance

The system should distinguish between the identity of the report facts and the identity of a rendered file.

## 16.1 Source hash

A deterministic hash is calculated from the canonical report data.

Conceptually:

```text
Canonical Report Data
        |
        v
canonical serialization
        |
        v
SHA-256
        |
        v
source_hash
```

The source hash means:

> These exact report facts produced the artifact.

The canonical serialization must be deterministic.

That requires fixed rules for:

- field ordering,
- number representation,
- date representation,
- currency representation,
- optional/null fields,
- schema versioning.

## 16.2 Artifact hash

Every rendered artifact receives its own hash.

Conceptually:

```text
PDF bytes
   |
   v
SHA-256
   |
   v
pdf_hash
```

Likewise:

```text
TXT -> txt_hash
XML -> xml_hash
JSON -> json_hash
```

The artifact hash means:

> These are the exact bytes of this artifact.

---

# 17. Source Hash vs Artifact Hash

One canonical report may produce several artifacts.

Example:

```text
Canonical report
source_hash = AAA
      |
      +--> PDF
      |     pdf_hash = BBB
      |
      +--> TXT
      |     txt_hash = CCC
      |
      +--> XML
            xml_hash = DDD
```

All three outputs represent the same underlying report facts.

The source hash provides continuity across formats.

The artifact hash identifies one exact rendered file.

---

# 18. Signing

Signing happens after rendering.

The signer should operate on artifacts, not on banking logic.

Conceptually:

```text
Canonical Report
       |
       v
Renderer
       |
       v
Artifact bytes
       |
       v
SHA-256
       |
       v
Artifact hash
       |
       v
Private-key signature
       |
       v
Signed artifact
```

The supplied Nordiska case specifically requires the native signing module to support:

- SHA-256 hashing of PDF content,
- signing with a private key,
- embedding signature information in PDF metadata,
- audit and traceability.

The signer should therefore know about:

```text
artifact bytes
hash
private key
signature metadata
```

It should not need to know:

```text
interest rates
tax law
currency conversion rules
customer balances
transaction semantics
```

---

# 19. Signing Boundary

Signing belongs at the artifact boundary:

```text
facts
  |
  v
render
  |
  v
artifact
  |
  v
SIGN
```

Not:

```text
database
  |
  v
SIGN
```

and not:

```text
interest aggregation
  |
  v
SIGN
```

If multiple artifact types are produced, each can be hashed and signed independently.

Example:

```text
Canonical Report
      |
      +--> PDF renderer
      |       |
      |       v
      |      PDF
      |       |
      |       v
      |    hash/sign
      |
      +--> XML renderer
              |
              v
             XML
              |
              v
           hash/sign
```

---

# 20. Audit Model

A report-generation audit record should conceptually preserve enough information to reproduce and verify what happened.

Potential audit information:

```text
report identifier
customer/account
report period
canonical schema version
source hash

jurisdiction adapter version
FX source/rate references where applicable

renderer type
renderer version
artifact hash

signing key identifier
signature
generation timestamp
status
```

The exact database schema is intentionally left open at this stage.

---

# 21. Regeneration and Idempotency

The system should avoid producing unnecessary duplicate artifacts.

A conceptual identity for a generated report could depend on:

```text
customer/account
period
source hash
report type
renderer version
```

If these are unchanged, the system can return an existing artifact rather than generate an identical report again.

However, because report generation should be cheap from canonical data, regeneration should remain possible.

The important invariant is:

> Regeneration from the same canonical facts must not silently change the meaning of the report.

---

# 22. Year-End Batch Processing

Year-end processing should be proactive rather than relying only on customers requesting reports one by one.

Conceptually:

```text
reporting period closes
        |
        v
finalize interest summaries
        |
        v
create canonical reports
        |
        v
dispatch report jobs
        |
        +--> worker
        +--> worker
        +--> worker
        +--> ...
        |
        v
render artifacts
        |
        v
sign artifacts
        |
        v
store / publish
```

The system should support scaling the number of workers without changing the underlying reporting rules.

---

# 23. Database Processing

The reporting system should take advantage of the database engine.

PostgreSQL can:

- filter rows,
- aggregate values,
- group by customer/account,
- use indexes,
- calculate sums and counts,
- return compact result sets.

Therefore the native reporting workers should not assume that they need to receive the entire transaction database.

Where possible:

```text
large ledger
    |
    v
PostgreSQL aggregation
    |
    v
small report input
    |
    v
native reporting pipeline
```

For the current interest-report use case, the native reporting stage may only need a compact annual interest summary.

---

# 24. Data Locality

Report workers do not need to run inside the PostgreSQL server process or directly manipulate database files.

PostgreSQL remains responsible for:

- physical storage,
- indexes,
- query execution,
- filtering,
- aggregation.

Workers consume query results through the database interface.

In a production system, reporting workers should generally be close to the database over a low-latency private network, rather than competing with the primary database process for CPU and I/O on the same machine.

The project implementation may approximate this with separate containers on the same private Docker network.

---

# 25. System Responsibilities

## Interest aggregation

Answers:

> How much interest was paid?

Input:

```text
ledger/account data
period
```

Output:

```text
native-currency interest summary
```

## FX conversion

Answers:

> What is this amount worth in the required reporting currency?

Input:

```text
native amount
currency
relevant date/rate information
```

Output:

```text
converted amount + provenance
```

## Jurisdiction adapter

Answers:

> What does this reporting jurisdiction require?

Input:

```text
generic financial facts
```

Output:

```text
canonical jurisdiction-specific report facts
```

## Renderer

Answers:

> How should these facts be represented?

Input:

```text
canonical report data
```

Output:

```text
PDF / TXT / CSV / JSON / XML / ...
```

## Signer

Answers:

> Can we prove that this exact artifact is genuine and unchanged?

Input:

```text
artifact bytes
```

Output:

```text
hash
signature
signed artifact / signature metadata
```

---

# 26. Architectural Overview

```text
                         BANK DATABASE / LEDGER
                                  |
                                  v
                         INTEREST AGGREGATION
                                  |
                                  v
                         Native-currency value
                                  |
                                  v
                      OPTIONAL FX CONVERSION
                                  |
                                  v
                       JURISDICTION ADAPTER
                                  |
                                  v
                       CANONICAL REPORT DATA
                                  |
                           source SHA-256
                                  |
                +-----------------+-----------------+
                |                 |                 |
                v                 v                 v
               PDF               TXT               XML
                |                 |                 |
                v                 v                 v
          artifact hash     artifact hash     artifact hash
                |                                   |
                v                                   v
              SIGNER                              SIGNER
                |                                   |
                v                                   v
          signed artifact                    signed artifact
```

---

# 27. What Belongs in the Native C/C++ Part

At the conceptual level, the native subsystem is well suited to:

- deterministic report processing,
- high-throughput batch rendering,
- PDF generation,
- hashing,
- signing,
- canonical serialization,
- validation,
- possibly regulator-specific serialization where useful.

The native subsystem should not own the entire banking application.

It should remain modular and callable by the surrounding .NET system.

---

# 28. What Should Stay Outside the Native Core

The native reporting subsystem should not become responsible for:

- authentication,
- BankID flows,
- web sessions,
- customer authorization,
- HTTP caching,
- portal UI,
- general account management,
- notification orchestration,
- full banking business logic,
- general transaction processing.

Those concerns belong to the surrounding application and service architecture.

---

# 29. Current Scope

For the first implementation, the smallest useful end-to-end flow is:

```text
account/year
     |
     v
interest summary in native currency
     |
     v
canonical report
     |
     v
PDF
     |
     v
SHA-256
     |
     v
signature
```

A Swedish reporting adapter can be added on top:

```text
interest summary
     |
     v
Swedish reporting rules
     |
     v
canonical Swedish report
     |
     +--> customer PDF
     |
     +--> future Skatteverket-compatible output
```

This satisfies the current Nordiska case while keeping the architecture open for:

- NOK,
- EUR,
- additional jurisdictions,
- new tax/report formats,
- new output formats.

---

# 30. Explicit Non-Goals at This Stage

This document intentionally does not decide:

- concrete C++ classes,
- executable names,
- REST endpoints,
- process invocation method,
- message queues,
- PDF library,
- cryptography library,
- database schema,
- database driver,
- deployment topology,
- exact JSON/XML schemas,
- exact Swedish tax implementation.

Those decisions should follow only after the conceptual boundaries are accepted.

---

# 31. Core Invariants

The design should preserve the following invariants:

1. Financial amounts use deterministic decimal/integer representations, never binary floating point for booked money.
2. A monetary value always carries a currency identity.
3. Currency scale is defined centrally from currency metadata rather than duplicated throughout the system.
4. Native-currency interest calculation is separate from FX conversion.
5. FX conversion is separate from jurisdiction-specific tax logic.
6. Jurisdiction-specific logic is separate from presentation.
7. PDF is an artifact, not the source of truth.
8. Canonical report data is the source representation for generated artifacts.
9. The same canonical facts produce the same source hash.
10. Every rendered artifact receives its own artifact hash.
11. Signing occurs on the exact artifact being delivered.
12. The signer does not need to understand banking or tax semantics.
13. Derived running totals do not replace the authoritative ledger.
14. Historical FX decisions required for reporting must remain auditable.
15. The architecture must allow new currencies, jurisdictions, and output formats without rewriting the interest aggregation core.

---

# 32. Design Summary

The system is best understood as:

```text
Financial facts
      |
      v
Generic interest summary
      |
      v
Optional transformations
      |
      v
Jurisdiction-specific report facts
      |
      v
Canonical report
      |
      v
Rendering
      |
      v
Hashing and signing
      |
      v
Delivery
```

The most important architectural decision is therefore:

> Separate what happened financially from how a country interprets it, how a document presents it, and how an artifact is cryptographically authenticated.

This keeps the system small at its core while allowing Nordiska to expand across currencies, jurisdictions, products, regulatory formats, and customer-facing delivery methods without redesigning the whole reporting pipeline.
