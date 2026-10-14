# Synthetic data tool — maintainer notes

For everyday setup and switching back, use [the quick-start guide](README.md).

The tool clones the prepared V2 schema, account types, roles and FAQ entries, applies V2 database permissions, and streams customers, accounts and ledger entries into PostgreSQL. Customer role IDs are resolved by name. The database container is selected through the `nordiska-v2` Compose project's `db` service.

Switching saves `NORDISKA_DATABASE` in `infra/v2/.env` and recreates `api`, `reporting-worker` and `pdf-signer`. The backend-only configuration on port 5031 is preserved when active. A failed switch restores the previous selection and attempts to recreate the previous services; rollback failure is reported. The MVP deployment configuration in `infra/docker-compose.yml` is separate.

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

Seeding requires an explicit `--customers` argument. With no arguments, the tool displays status. The reporting year defaults to **2025**. `--threads` controls native generation and defaults to the available CPU cores; benchmark commands fix it at four for a consistent configuration.

Append keeps existing synthetic records and avoids reusing their personal numbers. Reset, delete and seed operations move services back to `nordiska_v2` first when synthetic data is selected. Switch to `nordiska_synthetic` again after seeding.

The generator creates variable account counts per customer, `flex` accounts with 3.5% interest, fixed customer names and standard descriptions, and a simplified monthly interest fixture. `--stress-text` uses longer descriptions with Swedish characters. Record seed, year, thread count and actual customer/account/ledger counts. Parallel account and ledger ID allocation can vary with scheduling even with the same seed.

Native ingestion stages role and ledger rows in PostgreSQL temporary tables until parent customer/account COPY operations have committed, then inserts them with foreign-key checks. A failed native run exits with an error; resolve it and rerun a replacement seed to clear partial data.

Seed before requesting annual snapshots. Existing account/year requests can return a completed cached report. PDF counts depend on accounts and report eligibility. API Development startup can add demo/admin records after switching, so record database counts after startup. Seeding prepares the workload; submitting and measuring PDF reports requires separate benchmark tooling.

## Release binary and rebuilding

The release is version **1.0.0**, statically linked for Linux x86-64 and stored with its SHA-256 checksum. It does not depend on the host's Python, C++ runtime or shared libraries.

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

# Reads the prepared V2 schema/reference data and tests in a disposable PostgreSQL container
./tools/synthetic/tests/integration.sh
```

Regression checks cover argument validation, V2 discovery, workflow preservation, environment updates, switching rollback, reset/delete ordering and failure reporting. Integration checks seed 10,000 customers, append with long descriptions, and verify role mapping, foreign keys, balances and runtime permissions. The integration test removes its temporary container and files afterward; it does not seed or switch the running development stack.
