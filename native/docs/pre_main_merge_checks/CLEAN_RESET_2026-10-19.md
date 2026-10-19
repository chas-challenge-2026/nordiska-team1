# Clean-reset validation — 19 October 2026

Follow-up to [REPORT.md](REPORT.md), at revision `1fba684`.

## Reset performed

Removed this project's app, worker, signer, migrator and database containers; project network; both normal and synthetic PostgreSQL data; normal and synthetic PDF storage; signer socket volume; and generated project Docker images. Removed `infra/.env`, legacy `infra/v2/.env`, API/worker user secrets, all local .NET build outputs, frontend dependencies/build output, native PDF build directory (including fetched dependency checkouts), and the synthetic lock file.

Preserved tracked source, Dockerfiles/Compose configuration, the requested reports, IDE settings, unrelated containers and volumes, and shared package/image/build caches. This reproduces fresh project state, not a completely new operating system or an empty Docker installation. Docker reused shared build layers; this was not a `--no-cache` build.

## First-time setup

Ran the documented command from the repository root:

```sh
dotnet run --project ./backend/tools/Nordiska.DevSetup/Nordiska.DevSetup.csproj -- . start-stack
```

**Passed.** DevSetup generated all ten secrets with zero reused secrets, initialized a new PostgreSQL volume, applied all four modules' migrations, configured permissions, wrote four new API connection-string secrets, verified the API database role and built/started the complete stack. The combined app returned HTTP 200 at localhost:8080.

Immediately after initialization, the normal database had 2 customers, 4 accounts, 29 transactions and 0 generated documents; the synthetic database did not exist.

## Rerun results

| Check | Result |
|---|---|
| Backend locked restore and clean build/test | Passed: 607 tests initially passed and 74 PostgreSQL tests were skipped by default |
| Full API suite with PostgreSQL enabled | All 249 passed against a disposable database copied from the freshly migrated schema; total distinct backend tests passing: **681**, no remaining skipped coverage |
| Frontend clean dependency install | Passed; audit reported zero vulnerabilities |
| Frontend lint, TypeScript and production build | Passed; large JavaScript chunk warning remains |
| Frontend local-backend script tests | All five passed |
| Native host clean preset configure/build | Configure passed; full build reproduced the OpenSSL 4.0.3 signing-test compile failure described in the original report |
| Native host ingestion and C API suites | Both passed when built independently of the failing signing-test target |
| Native Docker tests | All three CTest suites passed on the Docker OpenSSL 3.3 toolchain |
| PDF renderer/parser matrix | All three renderers with both parsers produced 12 readable PDFs from the golden batch |
| Real signed generation | Passed against the newly initialized signer; `pdfsig` confirms valid SHA-256 signature covering the entire PDF |
| Synthetic mocked CLI/menu tests | 47 PASS checks |
| Synthetic native tests | Identity uniqueness/date/Luhn checks for all 37,620,000 identities, interest/calendar and ledger-format checks passed |
| Synthetic disposable ingestion | 10k seed plus append passed: 10,003 customers, 15,439 accounts, 734,036 ledger entries; deterministic generation, permissions, balances, indexing and failure recovery verified |
| Synthetic disposable switching | Passed real Compose recreation, rollback, worker replicas, document cleanup, deletion and normal-data preservation |
| Synthetic Compose configuration script | Reproduced the stale `api` service assumption failure described in the original report |
| Infrastructure platform guard | Passed |

## Fresh data and browser verification

Created a new synthetic database using:

```sh
./tools/syntheticDB/nordiska-synthetic-linux-x86_64 --customers 1000 --seed 42 --year 2025 --threads 4
./tools/syntheticDB/nordiska-synthetic-linux-x86_64 --switch nordiska_synthetic
```

This created 1,000 customers, 1,518 accounts and 71,254 transactions with no pre-existing documents. A customer returned by `--get-random-customer` logged in successfully through the browser. Overview, accounts, transactions, transfers, inbox, settings and help loaded without JavaScript runtime errors or unexpected authenticated API errors.

Both report modals passed generation, job polling and PDF download against the empty report store. Both downloaded PDFs are clean unsigned documents, matching the worker's default signing-disabled configuration. This resolves the earlier uncertainty caused by old cached tax reports: no invalid placeholder signature was present in either fresh browser-generated PDF.

Also ran the actual local frontend setup command (`frontend/scripts/setup-local-backend.mjs`). It created `.env.local` from the example, built/started the backend on port 5031, and successfully supported browser login and all seven customer pages through Vite on port 5173. Only expected unauthenticated `/api/auth/me` 401 responses were recorded.

## Findings remaining

1. The documented synthetic Compose validation still fails because it expects service/build target `api` instead of current `app`.
2. The native signing test still fails to compile with the README-compatible host OpenSSL 4.0.3; Docker OpenSSL 3.3 works.
3. Signed PDFs still produce `pdfsig` form-widget/bounding-box warnings despite valid signatures. Local demo certificate trust remains unconfigured, as expected.

No source fixes were made. Real BankID, production certificate trust, external timestamp availability, production deployment, exhaustive admin UI interaction, accessibility/mobile coverage and large-scale soak tests remain outside this rerun.

## End state and evidence

The standard combined app is restored on localhost:8080 using the newly initialized **normal database**. The new 1,000-customer synthetic dataset and its test PDFs remain available for further testing. Prior databases and PDFs were deleted. Test execution necessarily recreated generated secrets, dependencies, build outputs and Docker resources; the final checkout is not empty of generated files.

Disposable integration containers/databases were removed and the temporary Vite process was stopped. Shared caches and unrelated services remain untouched. Only these validation documents were edited in the working tree.

Detailed logs, TRX results, browser screenshots and generated PDFs are in `/tmp/nordiska-clean-validation/`. These are temporary local artifacts, not committed test fixtures. Do not publish the directory wholesale: some generated setup logs and fixture credential files are local development material.
