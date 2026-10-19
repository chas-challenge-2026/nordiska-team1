# Nordiska DevSetup

Run the commands from the repository root. The paths below work in Windows PowerShell, macOS and Linux.

## Set up the database

```shell
dotnet run --project ./backend/tools/Nordiska.DevSetup/Nordiska.DevSetup.csproj -- .
```

Creates the local secrets, starts PostgreSQL, applies migrations and permissions, and verifies the database connection.

## Set up and start everything

```shell
dotnet run --project ./backend/tools/Nordiska.DevSetup/Nordiska.DevSetup.csproj -- . start-stack
```

Performs the complete database setup and then builds and starts the full Docker environment.

## Repair database passwords

```shell
dotnet run --project ./backend/tools/Nordiska.DevSetup/Nordiska.DevSetup.csproj -- . repair-passwords
```

Synchronizes the PostgreSQL role passwords with the values stored in `infra/v2/.env` without deleting the database.

## Seed transactions

```shell
dotnet run --project ./backend/tools/Nordiska.DevSetup/Nordiska.DevSetup.csproj -- . seed-transactions [customerId] [batchSize]
```

Continuously creates test transactions for a customer until the command is stopped.

## Queue report jobs

```shell
dotnet run --project ./backend/tools/Nordiska.DevSetup/Nordiska.DevSetup.csproj -- . report-pressure-test [customerId] [accountId] [year] [batchSize] [delayMs]
```

Continuously inserts tax-report jobs to test the Reporting Worker under load.

## Request reports through the API

```shell
dotnet run --project ./backend/tools/Nordiska.DevSetup/Nordiska.DevSetup.csproj -- . direct-report-pressure-test [customerId] [accountId] [year] [batchSize] [delayMs] [baseUrl] [bearerToken]
```

Continuously requests tax reports through the API to test the complete report-generation flow.

## Benchmark Reporting Worker PDF throughput

```shell
dotnet run --project ./backend/tools/Nordiska.DevSetup/Nordiska.DevSetup.csproj -- . worker-pdf-benchmark [jobCount] [timeoutSeconds] [taxReportId]
```

Queues a finite batch against an existing tax-report snapshot, releases every job at the same time, and measures the real Reporting Worker path until all jobs complete or the timeout expires. Defaults to 10,000 jobs and a 300-second limit; when `taxReportId` is omitted, the newest snapshot is used.

The result includes completed and failed jobs, total PDFs per second, and p50/p95/p99 worker processing time. The command exits with code `0` only when every PDF completes within the time limit with no terminal failures. It measures the configuration currently running, so confirm whether PDF signing and timestamping are enabled when interpreting the result.

## Benchmark timestamp-signed PDFs

Start the signer and recreate the worker with the signed benchmark override:

```shell
docker compose --env-file ./infra/.env -f ./infra/docker-compose.yml -f ./infra/docker-compose.override.yml -f ./infra/docker-compose.signed-worker-benchmark.yml up -d --build pdf-signer reporting-worker
```

Then run:

```shell
dotnet run --project ./backend/tools/Nordiska.DevSetup/Nordiska.DevSetup.csproj -- . worker-timestamp-pdf-benchmark [jobCount] [timeoutSeconds] [taxReportId]
```

This mode requires every completed job to create a distinct stored document, which detects the timestamped output expected from the signer. It fails when the worker is still producing the same deterministic unsigned document, even if raw throughput meets the time target.

The worker defaults to 12 concurrent slots. Set `REPORTING_WORKER_CONCURRENCY` before starting Compose to benchmark another value without changing source files.
