# Synthetic data tool — maintainer notes

For everyday setup and switching back, use [the quick-start guide](README.md).

The tool clones the prepared V2 schema, account types, roles and FAQ entries, applies V2 database permissions, and streams customers, accounts and ledger entries into PostgreSQL. Customer role IDs are resolved by name. The database container is selected through the `nordiska-v2` Compose project's `db` service.

Compose files live in `infra/`; local secrets remain in `infra/v2/.env`. Switching atomically saves `NORDISKA_DATABASE` and `NORDISKA_REPORT_DOCUMENTS_VOLUME`, then recreates `app`, `reporting-worker` and `pdf-signer`. It preserves the worker replica count and verifies every running service replica uses the selected database. The backend-only configuration on port 5031 is preserved when active. A failed switch restores the exact previous environment file and attempts to recreate the previous services; rollback failure is reported. Other secrets and comments are preserved. The checkout lock prevents overlapping tool operations.

## Workload options

```bash
# Add customers using the same native engine
./synthetic-data-manager --customers 500 --append --seed 43 --year 2025 --threads 4

# Replace the dataset with long transaction descriptions
./synthetic-data-manager --customers 50 --stress-text --year 2025 --threads 4

# Generate without Docker or database writes (generator performance only)
./synthetic-data-manager --benchmark --customers 10000 --seed 42 --year 2025 --threads 4

# Reset to an empty cloned schema or delete the synthetic database
./synthetic-data-manager --reset
./synthetic-data-manager --drop

./synthetic-data-manager --help
```

Command-line seeding requires an explicit `--customers` argument. With no arguments in a terminal, the tool opens its native arrow-key menu; otherwise it displays status. `--menu` / `--tui` opens the menu explicitly and supports numbered choices with redirected input. The menu offers customer-count presets, CPU selection, reporting year, seed, long descriptions, replace/append modes and a confirmation before generation. It offers switching after generation. EOF cancels without selecting a destructive default. The reporting year defaults to **2025**. `--threads` controls native generation and defaults to the available CPU cores; benchmark commands fix it at four for a consistent configuration.

Append keeps existing synthetic records and avoids reusing their personal numbers. Reset, delete and seed operations move services back to `nordiska_v2` first when synthetic data is selected. Both source dumps are checked before replacement begins. Database operations never write to `nordiska_v2`, except that its normal application services can resume work after switching back. Switch to `nordiska_synthetic` again after seeding.

The generator creates variable account counts per customer, `flex` accounts with 3.5% interest, varied fictional customer names and standard descriptions, and a simplified monthly interest fixture. `--stress-text` uses longer descriptions with Swedish characters. Record seed, year, thread count and actual customer/account/ledger counts. Parallel account and ledger ID allocation can vary with scheduling even with the same seed.

Native ingestion stages role and ledger rows in PostgreSQL temporary tables until parent customer/account COPY operations have committed, then inserts them with foreign-key checks. A failed native run exits with an error; resolve it and rerun a replacement seed to clear partial data.

Seed before requesting annual snapshots. Existing account/year requests can return a completed cached report. PDF counts depend on accounts and report eligibility. App Development startup can add demo/admin records after switching, so record database counts after startup. Seeding prepares the workload; submitting and measuring PDF reports requires separate benchmark tooling.

## PDF storage

The normal database retains the existing `nordiska-v2_report_documents` volume. The synthetic database selects `nordiska-v2_synthetic_report_documents`. Switching overrides shell exports with the selected database and corresponding volume. The database volume and signer socket volume remain unchanged.

Reset, delete and replacement seeding retain both PDF volumes. Old synthetic files can become unreferenced after a reset; the benchmark workflow must clean only its own storage. Normal PDF storage must never be purged by benchmark cleanup. Services run outside Docker are not reconfigured by this tool.

## Release binary and rebuilding

The release is version **1.1.0**, statically linked for Linux x86-64 and stored with its SHA-256 checksum. It does not depend on the host's Python, C++ runtime or shared libraries.

```bash
./synthetic-data-manager --version
(cd tools/synthetic && sha256sum -c nordiska-synthetic-linux-x86_64.sha256)
```

Maintainers rebuild after changing [the C++ source](../../native/synthetic_generator/src/manager.cpp):

```bash
./tools/synthetic/build-linux-release.sh
```

The script uses a pinned GCC 14 container to build with C++23, release optimizations and static linking, then replaces the binary and updates its checksum. Only Docker is required on the build host. Commit the source, release binary and checksum together. This release targets Linux x86-64; other platforms require their own native build.

## Validation

```bash
# CLI and management regressions; Docker is mocked and no database is touched
./tools/synthetic/tests/run.sh

# Verify actual Compose paths and database/PDF-volume selections without starting services
python3 tools/synthetic/tests/compose-config.py

# Reads the prepared V2 schema/reference data and tests in a disposable PostgreSQL container
./tools/synthetic/tests/integration.sh

# Real switching/rollback and PDF isolation in a disposable Compose stack
./tools/synthetic/tests/switch-integration.sh
```

Regression checks cover argument validation, current Compose discovery, workflow preservation, exact environment rollback, running-container verification, reset/delete ordering, failed-dump preservation and COPY failure reporting. PTY tests exercise real arrow keys and Ctrl+C terminal restoration. The Compose configuration check verifies both frontend workflows, actual bind paths and PDF-volume isolation using fixture credentials. Test helpers require Python 3; the tool itself does not.

The database integration test seeds 10,000 customers, appends with long descriptions, and verifies varied names, role mapping, foreign keys, balances and runtime permissions. It reads only schema/reference data from the development database and writes to an isolated PostgreSQL container.

The switch integration test uses real Compose with lightweight app/worker/signer stand-ins, a unique project name and test-owned volumes. It verifies recreation, two worker replicas, frontend configuration, exact rollback, deletion ordering and preservation of normal PDF files. Both integration tests remove their temporary containers and files; they never seed or switch the running development stack.
