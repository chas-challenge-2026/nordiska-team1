# V2 benchmark setup

Set up the standard dataset for testing kontoöversikt and skatte-/ränteunderlag. **Run all commands from the repository root.** The included data tool runs on Linux x86-64.

## 1. Start V2

Prepare the local V2 stack using [DevSetup](local-development-setup.md). If your V2 stack is already running, continue to step 2.

## 2. Create and select the 10k database

```bash
./synthetic-data-manager --customers 10000 --seed 42 --year 2025 --threads 4 &&
./synthetic-data-manager --switch nordiska_synthetic &&
./synthetic-data-manager --status
```

The tool creates a separate `nordiska_synthetic` database and connects the API, reporting worker and PDF signer to it. **This replaces previous synthetic data. Your normal `nordiska_v2` database is preserved.**

The standard dataset simulates customer activity during **2025**:

| Data | Standard dataset |
|---|---:|
| Customers | 10,000 |
| Accounts | 15,436 |
| Average accounts per customer | 1.54, ranging from 1 to 6 |
| Ledger entries | About 734,000 |
| Database size before PDFs | About 148 MB |

Customers have different account counts, balances, deposits and withdrawals, with interest and tax entries. Accounts use the Flex product; names are placeholders. API startup may add demo records, so use `--status` to record actual counts.

## 3. PDF generation benchmark (batch runner)

Seeding the database prepares the dataset but does not generate PDFs. The dedicated standalone benchmark tool (`./report-benchmark` or `backend/tools/Nordiska.ReportBenchmark`) tests the **real application path from database to PDF**: it submits report requests through the V2 domain services (`IAccountStatementService` and `IAnnualTaxReportService`), waits for the deployed V2 reporting worker container(s) to process all jobs, monitors live queue progress, and records elapsed times, throughput, successful PDFs, and any failures.

The intended workload is:
- **Kontoöversikt (Account Statement):** One per account for `2025-01-01` through `2025-12-31`.
- **Skatte-/ränteunderlag (Annual Tax Report):** One for tax year `2025` per interest-bearing account (all seeded accounts earn interest).

In the standard **10k customer dataset** (15,436 accounts), this produces **30,872 target PDFs** (before demo records). In the **100k customer dataset** (154,028 accounts), this produces **308,056 target PDFs**.

### Run the benchmark

Run from the repository root:

```bash
# Run full benchmark using current signing mode and workers
./report-benchmark

# Test a small sample first (e.g. 100 accounts = 200 PDFs)
./report-benchmark --limit 100

# Alternatively, run via dotnet CLI
dotnet run --project backend/tools/Nordiska.ReportBenchmark -- --limit 100
```

### Testing different signing modes

PDF generation signing is controlled by environment variables in `infra/v2/.env` passed to the `reporting-worker` container:

| Mode | `NORDISKA_PDF_ENABLE_SIGNING` | `PDF_SIGNER_MODE` | Description |
|---|:---:|:---:|---|
| **Unsigned** | `0` | `plain` | Direct PDF rendering without cryptographic signing. Highest throughput. |
| **Plain Signed** | `1` | `plain` | Cryptographically signs PDFs using CMS/PKCS#7 via `pdf-signer` and SoftHSM. |
| **Timestamped** | `1` | `timestamp` | CMS/PKCS#7 signature with RFC 3161 TSA timestamp from local timestamping authority. |

You can test any signing mode directly using the `--signing` (or `--no-signing`) flag, which automatically configures `infra/v2/.env` and deploys the updated worker container:

```bash
# 1. Unsigned benchmark (or --no-signing)
./report-benchmark --signing none

# 2. Plain CMS signed benchmark
./report-benchmark --signing plain

# 3. Timestamped CMS signed benchmark
./report-benchmark --signing timestamp
```

Alternatively, configure `NORDISKA_PDF_ENABLE_SIGNING` and `PDF_SIGNER_MODE` in `infra/v2/.env` manually, then restart the worker:
```bash
docker compose -f infra/v2/docker-compose.yml -f infra/v2/docker-compose.override.yml up -d --no-deps reporting-worker
```

### Worker scaling

Compare worker replica counts using the same report type, signing mode, account limit and fresh dataset. Throughput and latency depend on the host, database, storage and signing service; record the configuration with each result.

You can scale workers directly via the benchmark CLI with `--workers <N>`:

```bash
# Run timestamp benchmark with 4 concurrent workers
./report-benchmark --signing timestamp --workers 4 --clean

# Run unsigned benchmark with 4 workers
./report-benchmark --no-signing --workers 4 --clean
```

Alternatively, scale workers using Docker Compose directly:

```bash
docker compose -f infra/v2/docker-compose.yml -f infra/v2/docker-compose.override.yml up -d --scale reporting-worker=4
```

### Options reference

- `--signing <unsigned|plain|timestamp>` / `-s`: Configures signing mode and redeploys workers.
- `--no-signing`: Shorthand for `--signing unsigned`.
- `--workers <N>` / `-w`: Scales `reporting-worker` container count (e.g., `--workers 4`). Waits for readiness before benchmark.
- `--limit <N>` / `-l`: Limits execution to first `N` accounts (default: all accounts).
- `--type <all|statements|tax>` / `-t`: Benchmarks only specific report types (default: `all`).
- `--clean`: Wipes database reporting tables, purges stored PDF files from the Docker volume (`nordiska-v2_report_documents`), and restarts workers before run (enabled by default).
- `--no-clean` / `--preserve`: Preserves existing reports in DB without wiping.
- `--year <YYYY>` / `-y`: Reporting tax year (default: `2025`).
- `--database <name>` / `--db`: Target PostgreSQL database (default: from `infra/v2/.env`).
- `--concurrency <N>` / `-c`: Parallel streams for submitting job requests via .NET DI (default: `16`).
- `--no-deploy`: Validates running worker database/signing configuration without modifying `.env` or scaling. Combine with `--no-clean` to preserve reports while existing workers run.
- `--timeout-minutes <N>`: Timeout waiting for workers to complete all jobs (default: `60`).
- `--poll-ms <N>`: Polling frequency in milliseconds (default: `1000`).
- `--flamegraph` / `--profile`: Generates `benchmark-flamegraph.txt` (collapsed stack) and `benchmark.speedscope.json` / `benchmark-speedscope.json` (speedscope profile) in repo root (default: enabled when worker telemetry is available).
- `--no-flamegraph`: Disables flamegraph file export.

### Optional phase profiles

The unchanged worker provides throughput, completion counts and job timestamp latency. Detailed native/managed phase profiles need separately installed worker instrumentation and its metrics table. The runner does not create that table; when measurements are unavailable it skips phase exports and prints an informational message.

When instrumentation is installed, profile exports include collapsed stacks and Speedscope files. See the [benchmark tool README](../backend/tools/Nordiska.ReportBenchmark/README.md#optional-worker-instrumentation).

For the separate native PDF renderer micro-benchmark (in-memory C++ benchmark without V2 database/worker overhead), see [its guide](../native/pdf_generator/src/benchmark/README.md). The data tool's `--benchmark` option measures data generation only.

## 4. Return to your normal database

```bash
./synthetic-data-manager --switch nordiska_v2
```

The services reconnect to your normal database. The synthetic database stays available for later use.

For another fresh run, repeat step 2. This clears previous synthetic report data; otherwise annual reports for the same account and year can be reused rather than generated again.
