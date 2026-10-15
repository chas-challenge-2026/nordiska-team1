# Synthetic data tool — maintainer notes

For everyday setup and switching back, use [the quick-start guide](README.md).

The tool clones the prepared V2 schema, account types, roles and FAQ entries, applies V2 database permissions, and streams customers, accounts and ledger entries into PostgreSQL. Customer role IDs are resolved by name. The database container is selected through the `nordiska-v2` Compose project's `db` service.

Compose files live in `infra/`; local secrets remain in `infra/.env` (legacy `infra/v2/.env` is also supported). Switching atomically saves `NORDISKA_DATABASE` and `NORDISKA_REPORT_DOCUMENTS_VOLUME`, then recreates `app`, `reporting-worker` and `pdf-signer`. It preserves the worker replica count and verifies every running service replica uses the selected database. Database switching uses the tool’s `compose.override.yml` without changing shared infrastructure files. The backend-only configuration on port 5031 is preserved when active. A failed switch restores the previous running app database and the exact previous environment file, including when they were initially mismatched; rollback failure is reported. Other secrets and comments are preserved. The checkout lock prevents overlapping tool operations.

## Workload options

```bash
# Add customers using the same native engine
./tools/syntheticDB/nordiska-synthetic-linux-x86_64 --customers 500 --append --seed 43 --year 2025 --threads 4

# Replace the dataset with long transaction descriptions
./tools/syntheticDB/nordiska-synthetic-linux-x86_64 --customers 50 --stress-text --year 2025 --threads 4

# Generate without Docker or database writes (generator performance only)
./tools/syntheticDB/nordiska-synthetic-linux-x86_64 --benchmark --customers 10000 --seed 42 --year 2025 --threads 4

# Reset to an empty cloned schema or delete the synthetic database
./tools/syntheticDB/nordiska-synthetic-linux-x86_64 --reset
./tools/syntheticDB/nordiska-synthetic-linux-x86_64 --drop

./tools/syntheticDB/nordiska-synthetic-linux-x86_64 --help
```

Command-line seeding requires an explicit `--customers` argument. With no arguments in a terminal, the tool opens its native arrow-key menu; otherwise it displays status. `--menu` / `--tui` opens the menu explicitly and supports numbered choices with redirected input. The compact main screen shows actual running app state and both database inventories. Its single switch action changes direction with the running app. Creation is available only when the synthetic database is missing. Its customer-count menu offers 100, 1000, 10k, 100k, 1m, 10m, MAX (derived from the personal-number capacity, currently 37,620,000) and a validated custom amount (1–37,620,000). After choosing a count, the settings screen offers an editable seed and Start creation. Seed values are validated as nonnegative signed 64-bit integers, default to 42 and are reused during the current manager session. Esc from settings/custom input returns to the count menu; Esc there returns to the main screen. Other generation options use their defaults and remain configurable through the CLI. Creation stays in the foreground without pause/resume or checkpoints. Live progress uses a structured callback from the native generator. The first min(500, requested) customers, including roles, accounts, ledger rows and sequence updates, commit before the initial total-completion estimate. Those customers count toward the requested total. The same worker pool then continues the rest of the run; measurement never restarts ingestion. The pool schedules 16 customers per work block, retains two reusable blocks per worker and applies backpressure when those buffers are occupied. Workers are capped at 64 (and the requested population); each block has four reusable COPY buffers. Creation computes personal numbers directly from seed and ordinal, without locks, retries or a growing set. Append reads existing identities into a fixed bitmap (about 4.5 MiB) and assigns skipped identities while scheduling blocks.

One persistent `docker exec -i … psql -X -v ON_ERROR_STOP=1` session handles the whole native ingestion run. Each transaction copies customers, roles, accounts and ledger rows directly in foreign-key order, avoiding temporary staging tables and their indexes. Later transactions target 50,000 ledger rows initially, with a measured-duration controller targeting about one second and clamping the row target to 10,000–100,000. An 8 MiB serialized-byte target and one-second assembly target apply independently. Boundaries round to whole work blocks; one unusually large block can exceed a target. The initial sample is the only fixed customer-count boundary. There is no whole-dataset transaction. Memory consists of the fixed pool plus one transaction buffer (the fixed initial sample or bounded sustained work), independent of the requested total. PostgreSQL index/cache/WAL memory is separate.

A stdout acknowledgement follows each successful COMMIT; no staging notice or pipe write counts as saved. All four tables roll back together on an ingestion failure, while earlier complete transactions remain. Private stderr capture retains PostgreSQL errors and is removed on close. The CLI reports setup, transfer/write, ingestion (including transfer and commit) and finalization separately; these timings overlap and must not be added as independent phases. Generation-only `--benchmark` measures the same workers and formatting without database writes. The UI separately shows generated, transferred and committed customer counts; transferred means flushed to the session pipe, not committed. UI refresh is every 100 ms; throughput sampling is every second using an EWMA with a four-second time constant. Slow windows with no commits lower the estimate's rate. Setup is excluded from extrapolation, queued work is included in remaining committed work, and the initial sample projection is replaced as sustained measurements accumulate. Query-index rebuilding, sequence updates and ANALYZE of all four generated tables must succeed before Complete/100%; ready/saved counters can reach their totals while finalization is still running. For deferred indexes, the total ETA reserves an approximate 20% of the indexed initial-sample projection for finalization. This is a calibration-based allowance, not a measured index-build duration. Once all data commits, the UI shows “finishing database” with the current rebuild stage; completion remains below 100% until indexes, sequences and statistics succeed.

Confirmed deletion returns services to NORMAL and removes the database, then creation becomes available again. The TUI has no empty/reset or append action; the advanced command-line append operation remains available. Operations use spinners and brief result messages, then refresh state while retaining selection where possible. Failed state reads offer Retry; an offline app is labeled OFFLINE and can be started on an existing database. Login pages preserve UTF-8 customer names and return with Enter or Esc. EOF cancels without selecting a destructive default. The reporting year defaults to **2025**. `--threads` controls native generation and defaults to the available CPU cores; benchmark commands fix it at four for a consistent configuration.

Append keeps existing synthetic records and avoids reusing their personal numbers, including older or differently seeded fixtures. It checks remaining identity capacity before inserting any new customers. Reset, delete and seed operations move services back to `nordiska_v2` first when synthetic data is selected or a running service uses it. Both source dumps are checked before replacement begins. Database operations never write to `nordiska_v2`, except that its normal application services can resume work after switching back. Switch to `nordiska_synthetic` again after seeding.

The generator creates variable account counts per customer, `flex` accounts with a 3.5% annual rate, varied fictional customer names and standard descriptions, and daily interest accrual with monthly payouts. Activity is generated in chronological date order across each full calendar month. Deposits and withdrawals affect their dated day’s closing balance. Interest uses actual month lengths and 365/366 days per reporting year, with no daily rounding; only the monthly payout is rounded to öre. Interest and 30% withholding tax post on the actual last day of the month, and the net payout earns interest from the next month. Balances and payout/tax rounding use integer öre. These are explicit fixture conventions, not a reproduction of a particular bank product’s value dates or payout terms. `--stress-text` uses longer descriptions with Swedish characters. Record seed, year, thread count and actual customer/account/ledger counts. Account IDs reserve six positions per customer; ledger IDs reserve 512 positions per account (the current workload has at most 445 rows). Unused positions are intentional gaps. IDs and generated rows now reproduce across thread counts. Workers accumulate counts locally and update shared generation progress once per work block.

The personal-number domain contains **37,620,000** combinations: 37,620 valid Gregorian birth dates from 1905-01-01 through 2007-12-31, each with individual numbers 000–999. The fourth suffix digit is the calculated Luhn checksum. Dates include leap days; customers are aged 18–120 on 2025-12-31. The earliest birth date reaches age 120 during 2025; the latest turns 18 on 2025-12-31. This range is fixed independently of `--year`. The seed chooses an offset modulo capacity, then customer ordinals count through the domain, wrapping at its end once without repeating. Personal-number assignment during creation is independent of thread scheduling. UI and CLI requests above capacity are rejected before database changes; exhaustion is an explicit error, never an unbounded retry loop.

Native ingestion retains primary keys, unique indexes and all foreign-key constraints throughout. For creation of at least 10,000 customers into an empty ledger table, it saves eligible non-unique ledger index definitions, commits the complete 500-customer sample with all indexes present, then drops those query indexes for sustained loading. Each original index is rebuilt separately during finalization and its exact definition and validity are verified. Indexes supporting constraints, replica identity, clustering, extension membership, comments or custom statistics are excluded. Append operations and smaller runs keep all indexes; `--keep-indexes` disables deferral for diagnostics and comparisons. Each transaction copies in parent-before-child order with foreign-key checks.

On a handled ingestion or finalization failure, the tool attempts to restore deferred indexes through a fresh session and still returns failure. Restoration failure is explicitly reported. Abrupt process termination or an unavailable database can leave a partial dataset with query indexes absent; rerun replacement creation before using it. No database-wide durability or WAL settings are changed. A failed native run exits with an error; resolve it and rerun a replacement seed to clear partial data.

Seed before requesting annual snapshots. Existing account/year requests can return a completed cached report. PDF counts depend on accounts and report eligibility. App Development startup can add demo/admin records after switching, so record database counts after startup. Seeding prepares the workload; submitting and measuring PDF reports requires separate benchmark tooling.

## PDF storage

The normal database retains the existing `nordiska-v2_report_documents` volume. The synthetic database selects `nordiska-v2_synthetic_report_documents`. Switching overrides shell exports with the selected database and corresponding volume. The database volume and signer socket volume remain unchanged.

Reset, delete and replacement seeding retain both PDF volumes. Old synthetic files can become unreferenced after a reset. The document cleanup action (also `--delete-documents`) removes the fixed synthetic PDF volume contents, generated-document/link rows and PDF jobs, including queued jobs. Snapshots and banking data are retained. It briefly stops only synthetic app/worker/signer containers and restarts those same IDs, preserving a potentially stale saved selection. Normal services with synthetic mounts or unknown containers using that volume make cleanup fail before deletion. Metadata deletion is transactional; file cleanup failures leave retryable orphans and still restart stopped services. A read-only helper using the already-present database image detects retained files even if the synthetic DB was deleted. No volume is created by status when the synthetic volume is absent. Normal PDF storage must never be purged by benchmark cleanup. Services run outside Docker are not reconfigured by this tool.

## Release binary and rebuilding

The release is version **1.3.0**, statically linked for Linux x86-64 and stored with its SHA-256 checksum. It does not depend on the host's Python, C++ runtime or shared libraries.

```bash
./tools/syntheticDB/nordiska-synthetic-linux-x86_64 --version
(cd tools/syntheticDB && sha256sum -c nordiska-synthetic-linux-x86_64.sha256)
```

Maintainers rebuild after changing [the C++ source](src/manager.cpp):

```bash
./tools/syntheticDB/build-linux-release.sh
```

The script uses a pinned GCC 14 container to build with C++23, release optimizations and static linking, then replaces the binary and updates its checksum. Only Docker is required on the build host. Commit the source, release binary and checksum together. This release targets Linux x86-64; other platforms require their own native build.

## Validation

```bash
# Native unit tests: identity domain, daily interest and ledger formatting
./tools/syntheticDB/tests/run.sh --native

# CLI and management regressions; Docker is mocked and no database is touched
./tools/syntheticDB/tests/run.sh

# Verify actual Compose paths and database/PDF-volume selections without starting services
python3 tools/syntheticDB/tests/compose-config.py

# Reads the prepared V2 schema/reference data and tests in a disposable PostgreSQL container
./tools/syntheticDB/tests/integration.sh

# Real switching/rollback and PDF isolation in a disposable Compose stack
./tools/syntheticDB/tests/switch-integration.sh
```

Regression checks cover argument validation, current Compose discovery, workflow preservation, exact environment rollback, running-container verification, pending database selection and actual PDF mount reporting, reset/delete ordering, failed-dump preservation and COPY failure reporting. PTY tests exercise the 80x24 screen, actual state, hidden actions, one-step switches, spinner animation, refreshed switch direction, details/Esc navigation, creation cancellation, selection preservation and terminal restoration. The Compose configuration check verifies both frontend workflows, actual bind paths and PDF-volume isolation using fixture credentials. Test helpers require Python 3; the tool itself does not. Private PostgreSQL mocks/observers, API-login validation and the independent SQL interest reference live under `tests/helpers/`. The benchmark runner is separate from regression tests and writes generated results outside the checkout by default; result snapshots are not part of the source tree.

The database integration test seeds 10,000 customers, observes external committed counts at session acknowledgements, verifies the initial 500 and subsequent workload-budget commits, exact seed workload counts, daily-interest/tax agreement with an independent SQL reference, actual month-end payouts including February 29, completed ANALYZE and total-completion ETA, appends with long descriptions, and checks varied names, role mapping, foreign keys, balances and runtime permissions. It also injects errors in initial/later data transactions and final index rebuilding to check rollback, query-index restoration through a fresh session and honest completion reporting. It also compares exact customer/account/ledger rows across one and four threads. It reads only schema/reference data from the development database and writes to an isolated PostgreSQL container.

It also verifies document counts and random-customer login details. When the local `nordiska-v2-app:latest` image is available, it starts a disposable app with all five database connections pointing at the isolated synthetic database, then checks email/password login and the authenticated profile. Set `SYNTHETIC_TEST_APP_IMAGE` to use another already-built app image. This optional endpoint check does not build or pull an app image and does not alter running development services.

The switch integration test uses real Compose with lightweight app/worker/signer stand-ins, a unique project name and test-owned volumes. It verifies recreation, two worker replicas, frontend configuration, exact rollback, deletion ordering, synthetic PDF cleanup and preservation of normal PDF files. Both integration tests remove their temporary containers and files; they never seed or switch the running development stack.

## Performance measurements

With seed 42, year 2025 and four generator threads, deferring eligible ledger query indexes reduced total native ingestion time by 20–23% in disposable PostgreSQL tests:

| Customers | Ledger rows | Keep indexes throughout | Optimized |
|---:|---:|---:|---:|
| 10,000 | 734,199 | 11.61s | 8.91s |
| 30,000 | 2,191,123 | 30.13s | 24.22s |

These historical measurements use the earlier simplified interest fixture. The corrected daily-accrual fixture produces 733,920 ledger rows for 10,000 customers at seed 42/year 2025; account counts remain 15,436. Both measured paths include generation, commits, sequence updates and ANALYZE; optimized timings also include rebuilding and verifying the deferred indexes. These are individual runs on shared storage, with schema cloning outside the native timing. Primary keys and foreign keys remain enabled. The remaining load cost includes COPY, constraint checks, primary-key maintenance and WAL/storage work.

To repeat generation and real ingestion separately:

```bash
bash tools/syntheticDB/benchmark.sh
```

This builds a standalone native driver with the same pinned compiler, reads only development schema/reference data, and writes to a test-owned PostgreSQL container. Results (timings, CPU metadata, counts, WAL/checkpoint statistics and transport/COPY/index experiments) remain in the printed temporary directory. It never switches services or writes to `nordiska_v2`. Set `SYNTHETIC_BENCH_OUTPUT`, `SYNTHETIC_BENCH_GENERATION_SIZES`, `SYNTHETIC_BENCH_INGESTION_SIZES`, `SYNTHETIC_BENCH_THREADS` or `SYNTHETIC_BENCH_INDEX_MODES` to choose result storage and sizes. Use `SYNTHETIC_BENCH_SOURCE` and `SYNTHETIC_BENCH_BINARY` together to compare a saved source/release baseline. Run comparisons sequentially on the same storage; record system load, seed, year, threads, actual row counts and finalization. There is no guarantee of identical database throughput at larger scales: indexes, WAL, checkpoints and storage matter.

To compare the complete ingestion paths while holding generation threads fixed:

```bash
SYNTHETIC_BENCH_THREADS=4 \
SYNTHETIC_BENCH_INDEX_MODES='keep-indexes optimized' \
bash tools/syntheticDB/benchmark.sh
```

Both modes include final sequence updates and ANALYZE; optimized mode also includes restoring query indexes. The complete 500-customer initial sample retains all indexes in both modes.
