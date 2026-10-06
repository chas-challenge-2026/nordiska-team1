# Account Statement PDF Pipeline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a standalone, asynchronous, least-privilege account-statement flow that creates an immutable Reporting snapshot for a user-selected period of at most twelve months and renders it through the existing native PDF pipeline.

**Architecture:** The authenticated API reads Banking data, calculates balances, and transactionally stores a Reporting-owned header, ordered entry snapshots, and a dedicated job. The separate Worker reads only Reporting tables, verifies the snapshot hash, renders `account_statement` through the existing native batch API, writes the shared private volume, records document metadata, and completes the job.

**Tech Stack:** .NET 8, ASP.NET Core, EF Core 8, PostgreSQL 15, C# Worker Service, existing native C++ PDF C API, Docker Compose.

**Spec:** `docs/superpowers/specs/2026-10-06-account-statement-pipeline-design.md`

## Global Constraints

- Add no automated tests and do not run `dotnet test`, per explicit user request.
- Run compilation and inspect additive migration SQL only; runtime and Scalar validation belong to the user.
- Make no Git commits, pushes, resets, or destructive cleanup.
- Preserve all unrelated staged, unstaged, native, signer, and PDF-pipeline changes already in the checkout.
- Use a dedicated controller, snapshot tables, job table, and document-link table; reuse only stable shared infrastructure.
- The Worker must receive no Banking privileges and no mutation rights on account-statement snapshots or entries.
- Accept an inclusive user period, reject future end dates, and reject periods reaching or exceeding `FromDate.AddMonths(12)`.

## Review Focus

- Boundary periods: same-day requests and exactly January 1 through December 31 must be accepted; periods longer than twelve months must return `400`.
- Ownership: a foreign `AccountId`, job ID, or statement ID must return `404` without revealing another customer's data.
- Snapshot determinism: entries with equal `CreatedAt` must be ordered by source ledger-entry ID, and PostgreSQL timestamp precision must not break hash verification.
- Balance integrity: opening balance, cumulative balance-after values, and closing balance must reconcile with the authoritative current balance and non-planned ledger entries.
- Duplicate requests: a sequential request matching a `Pending` or `Processing` job must reuse it; a request after `Completed` must create a fresh snapshot.

---

### Task 1: Domain snapshot and application contracts

**Files:**
- Create: `backend/src/Modules/Reporting/Domain/AccountStatement.cs`
- Create: `backend/src/Modules/Reporting/Domain/AccountStatementEntry.cs`
- Create: `backend/src/Modules/Reporting/Domain/AccountStatementJob.cs`
- Create: `backend/src/Modules/Reporting/Domain/AccountStatementDocument.cs`
- Create: `backend/src/Modules/Reporting/Application/IAccountStatementSourceQuery.cs`
- Create: `backend/src/Modules/Reporting/Application/IAccountStatementRepository.cs`
- Create: `backend/src/Modules/Reporting/Application/IAccountStatementJobRepository.cs`
- Create: `backend/src/Modules/Reporting/Application/IAccountStatementCompletionRepository.cs`
- Create: `backend/src/Modules/Reporting/Application/IAccountStatementQueryService.cs`
- Create: `backend/src/Modules/Reporting/Application/AccountStatementHashPayload.cs`
- Create: `backend/src/Modules/Reporting/Application/IAccountStatementSnapshotVerifier.cs`
- Create: `backend/src/Modules/Reporting/Application/AccountStatementService.cs`

**Interfaces:**
- Produces: `AccountStatement`, `AccountStatementEntry`, `AccountStatementJob`, `AccountStatementDocument`.
- Produces: `IAccountStatementService.RequestAsync(long customerId, long accountId, DateOnly fromDate, DateOnly toDate, CancellationToken)` returning statement ID, job ID, status, and creation time.
- Produces: `IAccountStatementSourceQuery.GetAsync(...)` returning customer/account metadata, current balance, and ordered non-planned source entries.
- Produces: repository, claim/failure, completion, status/download, hash, and verifier contracts parallel to the annual-tax-report contracts but typed for account statements.

- [ ] **Step 1: Add immutable domain entities**

Store period dates, display metadata, balances in minor units, snapshot timestamp, schema version, payload hash, and a private ordered entry collection. `AccountStatementJob` owns the same lease/retry transitions as `TaxReportJob`, with account-statement-specific status constants.

- [ ] **Step 2: Add source and persistence contracts**

Define source records carrying `SourceLedgerEntryId`, `CreatedAt`, `Type`, `Description`, and decimal `Amount`; define claimed-job and completed-document records with account-statement IDs.

- [ ] **Step 3: Add deterministic payload hashing and verification**

Serialize every header field and entry field in sequence order, normalize timestamps to PostgreSQL microsecond precision, hash with SHA-256, and compare using `CryptographicOperations.FixedTimeEquals`.

- [ ] **Step 4: Add request orchestration**

Validate ownership-period inputs, reuse a matching in-flight job, calculate opening/closing and balance-after minor amounts, build the immutable snapshot, compute its hash, and request transactional persistence.

- [ ] **Step 5: Compile the Reporting project**

Run: `dotnet build backend/src/Modules/Reporting/Nordiska.Modules.Reporting.csproj --no-restore`

Expected: build succeeds with zero errors.

### Task 2: Reporting persistence and Banking source query

**Files:**
- Create: `backend/src/Modules/Reporting/Infrastructure/Db/Queries/AccountStatementSourceQuery.cs`
- Create: `backend/src/Modules/Reporting/Infrastructure/AccountStatementRepository.cs`
- Create: `backend/src/Modules/Reporting/Infrastructure/AccountStatementJobRepository.cs`
- Create: `backend/src/Modules/Reporting/Infrastructure/AccountStatementCompletionRepository.cs`
- Create: `backend/src/Modules/Reporting/Infrastructure/AccountStatementQueryService.cs`
- Modify: `backend/src/Modules/Reporting/Infrastructure/Db/DependencyInjection.cs`

**Interfaces:**
- Consumes: all Task 1 contracts and domain types.
- Produces: atomic snapshot/job creation, lease-safe job claiming, failure/retry, completion metadata, authenticated status, and authenticated PDF download implementations.

- [ ] **Step 1: Implement the source query**

Use the API's database connection to read the owned Banking account and non-planned ledger entries. Order entries by `CreatedAt`, then `Id`. Return `null` when account and customer do not match.

- [ ] **Step 2: Implement snapshot and in-flight lookup persistence**

Query matching account/period snapshots joined to jobs in `Pending` or `Processing`; otherwise insert the header, all entry rows, and a pending job in one transaction.

- [ ] **Step 3: Implement lease-safe job persistence**

Use `FOR UPDATE SKIP LOCKED`, `Status/AvailableAt`, expired leases, and the existing Worker retry settings exactly as the tax repository does.

- [ ] **Step 4: Implement completion and download queries**

Insert or reuse `GeneratedDocument` with document type `AccountStatement`, insert `AccountStatementDocument`, and complete only the job locked by the current Worker. Status/download queries must join through the statement's `CustomerId` before returning data or reading storage.

- [ ] **Step 5: Register all services**

Register account-statement services, repositories, source query, hasher, verifier, and query service alongside the existing annual-tax-report registrations.

- [ ] **Step 6: Compile the Reporting project**

Run: `dotnet build backend/src/Modules/Reporting/Nordiska.Modules.Reporting.csproj --no-restore`

Expected: build succeeds with zero errors.

### Task 3: EF mappings, indexes, and additive migrations

**Files:**
- Create: `backend/src/Modules/Reporting/Infrastructure/Db/SqlConfigurations/AccountStatementConfiguration.cs`
- Create: `backend/src/Modules/Reporting/Infrastructure/Db/SqlConfigurations/AccountStatementEntryConfiguration.cs`
- Create: `backend/src/Modules/Reporting/Infrastructure/Db/SqlConfigurations/AccountStatementJobConfiguration.cs`
- Create: `backend/src/Modules/Reporting/Infrastructure/Db/SqlConfigurations/AccountStatementDocumentConfiguration.cs`
- Modify: `backend/src/Modules/Reporting/Infrastructure/Db/ReportingDbContext.cs`
- Modify: `backend/src/Modules/Banking/Infrastructure/Db/SqlConfigurations/LedgerEntryConfiguration.cs`
- Create: generated Reporting migration named `AddAccountStatementFlow`
- Create: generated Banking migration named `AddLedgerEntryStatementRangeIndex`

**Interfaces:**
- Consumes: Task 1 domain types.
- Produces: four new Reporting tables and one Banking composite index without altering or dropping existing application tables.

- [ ] **Step 1: Map entities and relationships**

Configure Reporting table names, keys, required/max-length fields, restricted foreign keys, `(AccountStatementId, SequenceNumber)` uniqueness, account/period lookup, job claim/lease indexes, and document lookup indexes.

- [ ] **Step 2: Add the Banking range index**

Add an EF index on `(AccountId, CreatedAt)` with a stable database index name. Keep the existing `AccountId` index unless the generated SQL demonstrates it is safely redundant and the user separately authorizes removal.

- [ ] **Step 3: Add DbSets and apply configurations**

Expose the four account-statement entities from `ReportingDbContext`; rely on `ApplyConfigurationsFromAssembly` for mapping discovery.

- [ ] **Step 4: Generate both migrations**

Run the Reporting and Banking `dotnet ef migrations add` commands from `backend`, targeting their existing migration directories and contexts.

- [ ] **Step 5: Generate and inspect migration SQL**

Confirm SQL contains only the four new Reporting tables, their indexes/foreign keys, and the new Banking index. It must contain no `DROP TABLE`, no removal of existing columns/indexes, and no modification of native or signer files.

### Task 4: Dedicated API controller and contracts

**Files:**
- Create: `backend/src/Modules/Reporting/Contracts/Requests/AccountStatementDtos.cs`
- Create: `backend/src/Modules/Reporting/Contracts/Responses/AccountStatementResponses.cs`
- Create: `backend/src/Nordiska.FrontendApi/Endpoints/Reporting/AccountStatementController.cs`

**Interfaces:**
- Consumes: `IAccountStatementService` and `IAccountStatementQueryService` from Task 1.
- Produces: `POST /api/reports/account-statements`, `GET /api/reports/account-statements/jobs/{jobId}`, and `GET /api/reports/account-statements/{accountStatementId}/pdf`.

- [ ] **Step 1: Add request and response DTOs**

Use `AccountId`, `FromDate`, and `ToDate`; return account-statement ID, job ID, status, and creation time for accepted requests.

- [ ] **Step 2: Add the authenticated controller**

Resolve `CustomerId` only from claims, return `202` for create, `400` for invalid periods, `404` for missing/foreign resources, and stream the completed PDF using stored content type and file name.

- [ ] **Step 3: Compile the API project**

Run: `dotnet build backend/src/Nordiska.FrontendApi/Nordiska.FrontendApi.csproj --no-restore`

Expected: build succeeds with zero errors.

### Task 5: Native mapper and Worker processing

**Files:**
- Create: `backend/src/Nordiska.Reporting.Worker/AccountStatementNativeMapper.cs`
- Create: `backend/src/Nordiska.Reporting.Worker/AccountStatements/AccountStatementRenderPipeline.cs`
- Create: `backend/src/Nordiska.Reporting.Worker/AccountStatements/AccountStatementJobProcessor.cs`
- Modify: `backend/src/Nordiska.Reporting.Worker/AnnualTaxReportNativeMapper.cs`
- Modify: `backend/src/Nordiska.Reporting.Worker/Program.cs`
- Modify: `backend/src/Nordiska.Reporting.Worker/Worker.cs`

**Interfaces:**
- Consumes: Task 1 snapshot/job contracts and the existing `IPdfBatchGenerator`, `IReportDocumentStorage`, and Worker options.
- Produces: native `account_statement` JSON, validated PDF bytes, shared-volume storage, metadata/link persistence, and fair polling of one tax job plus one account-statement job per loop.

- [ ] **Step 1: Make the shared native envelope document-agnostic**

Change only the managed JSON envelope's `Document` property to accept both annual-tax and account-statement document records without changing emitted annual-tax JSON.

- [ ] **Step 2: Implement account-statement native mapping**

Emit `document_id = account_statement_{Id}`, `kind = account_statement`, period display, Swedish money formatting, `amount_minor`, and transactions in snapshot sequence order.

- [ ] **Step 3: Implement rendering validation**

Load and hash-verify the snapshot, generate one native batch, require matching customer ID and document ID, and require `%PDF-` bytes. Name files `kontoutdrag-{from:yyyyMMdd}-{to:yyyyMMdd}-{accountNumber}.pdf`.

- [ ] **Step 4: Implement job processing and DI**

Mirror tax processing for SHA-256, storage key, completion, retry, and failure. Register mapper, pipeline, and processor.

- [ ] **Step 5: Update the Worker loop fairly**

Attempt one annual-tax job and one account-statement job per scope iteration; delay only when neither processor handled a job.

- [ ] **Step 6: Compile the Worker project**

Run: `dotnet build backend/src/Nordiska.Reporting.Worker/Nordiska.Reporting.Worker.csproj --no-restore`

Expected: build succeeds with zero errors; the existing local Windows native-runtime warning is acceptable.

### Task 6: Least-privilege grants and final static verification

**Files:**
- Modify: `infra/v2/postgres/permissions/post-migration-permissions.sql`
- Modify: `infra/v2/postgres/permissions/post-migration-permissions.development.sql`

**Interfaces:**
- Consumes: Task 3 table and sequence names.
- Produces: Worker privileges restricted to snapshot reads, job reads/updates, document/link reads/inserts, and the generated-document identity sequence.

- [ ] **Step 1: Extend Worker table grants**

Grant `SELECT` on account statements, entries, jobs, and links; `UPDATE` only on account-statement jobs; and `INSERT` only on account-statement document links in addition to the existing generated-document insert.

- [ ] **Step 2: Tighten sequence usage**

Replace broad Reporting sequence usage with usage on the generated-document identity sequence required by both PDF flows.

- [ ] **Step 3: Run solution build only**

Run: `dotnet build backend/np.sln --no-restore`

Expected: zero build errors. Do not run tests.

- [ ] **Step 4: Review the complete diff**

Confirm only account-statement, additive migration, permission, Worker dispatch, shared envelope, and approved design/plan files changed for this feature. Preserve all pre-existing user/native modifications and make no commit.
