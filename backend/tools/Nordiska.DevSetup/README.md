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
