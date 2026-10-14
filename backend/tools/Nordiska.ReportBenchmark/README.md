# Nordiska PDF Reporting Benchmark

Standalone benchmarking tool measuring the **real application path from database to PDF** for Kontoöversikt (Account Statements) and Skatte-/ränteunderlag (Annual Tax Reports).

## Overview

Unlike micro-benchmarks or synthetic generators, this tool tests the complete end-to-end production pipeline:
1. **Database Discovery**: Discovers accounts and customers directly in PostgreSQL.
2. **Domain Service Processing**: Executes real `.NET` domain services (`IAccountStatementService` and `IAnnualTaxReportService`) which query ledger entries, calculate period balances/interest/taxes, create snapshots, and enqueue jobs in `reporting.account_statement_jobs` and `reporting.tax_report_jobs`.
3. **Queue Drain & Rendering**: The deployed `reporting-worker` Docker container(s) claim jobs via `FOR UPDATE SKIP LOCKED`, execute the C# render pipelines, call the native C++ PDF engine (`libpdf_generator.so`), optionally sign via `pdf-signer` (SoftHSM / TSA), and store generated PDFs in `/var/lib/nordiska/report-documents`.
4. **Live Metrics**: Measures submission throughput, worker drain throughput, latency, failure rates, and end-to-end processing times.

## Quick Start

From the repository root:

```bash
# Run 100-account test without signing using 2 workers
./report-benchmark --limit 100 --signing none --workers 2

# Run full benchmark with timestamp signing and 8 workers
./report-benchmark --signing timestamp --workers 8

# Alternatively, run via dotnet CLI
dotnet run --project backend/tools/Nordiska.ReportBenchmark -- --signing plain --workers 4
```

## Options Reference

| Option | Shorthand | Description | Default |
|---|:---:|---|---|
| `--signing <mode>` | `-s` | Worker signing mode: `unsigned` (or `none`), `plain`, `timestamp` | Current mode in `.env` |
| `--no-signing` | | Shorthand for `--signing unsigned` | |
| `--workers <N>` | `-w` | Deploys/scales `reporting-worker` containers to N instances with readiness check | Current deployed count |
| `--limit <N>` | `-l` | Limits run to first N active accounts (e.g. `--limit 100`) | `0` (all accounts) |
| `--clean` | | Cleans database reporting tables and purges stored PDF files from volume before run | `true` |
| `--no-clean` | | Skips database & storage volume cleanup to preserve existing reports | `false` |
| `--year <YYYY>` | `-y` | Statement period and tax reporting year | `2025` |
| `--concurrency <N>` | `-c` | Parallel streams submitting requests via .NET DI | `16` |
| `--database <name>` | `--db` | PostgreSQL database name | `NORDISKA_DATABASE` from `.env` |
| `--no-deploy` | | Validates running workers match requested DB/signing without modifying containers | `false` |
| `--timeout-minutes <N>` | | Max queue drain wait time in minutes | `60` |
| `--poll-ms <N>` | | DB polling interval in milliseconds | `1000` |

## Primary Metrics Reported

- **Total Batch Duration**: Wall-clock time from START of run to END when all documents are generated on disk.
- **Overall Batch Throughput**: Total PDFs completed per second across the entire batch run (`Total PDFs / Total Time`).
- **Per-Document Latency**: Job processing time per document: median (p50), 95th percentile (p95), and average.
- **Active Worker Span**: Millisecond-accurate timestamp span (`MIN(StartedAt)` to `MAX(CompletedAt)`) measured directly from PostgreSQL system clocks, free of polling discretization error.

## Optional worker instrumentation

The unchanged Reporting Worker supports overall throughput, job completion/failure counts and latency measured from job timestamps. No worker source changes or telemetry table are required to run this tool.

Detailed phase measurements and profile exports require separately installed worker instrumentation and its `reporting.job_performance_metrics` table and permissions. This tool does not create that schema. If the table is absent or has no measurements for the run, phase profiles are skipped with an informational message. Existing measurements are cleared by `--clean` when the optional table exists.

With instrumentation available, `--profile` exports collapsed stacks and Speedscope profiles; `--no-profile` disables exports. Phase measurements cover claims, snapshot loading, JSON mapping, native rendering/signing, managed hashing, storage and completion.

Cleanup is enabled by default and deletes reporting data and stored PDFs. Use a disposable benchmark database. `--no-clean` preserves existing reports; cached results may then reduce the number of freshly rendered PDFs. `--no-deploy` leaves deployment unchanged, so combine it with `--no-clean` to avoid cleanup while existing workers are running.
