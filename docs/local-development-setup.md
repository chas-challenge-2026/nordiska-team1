<div align="center">

# Local development setup

### Windows · macOS · Linux

[Windows](#windows) · [macOS](#macos) · [Linux](#linux) · [Common commands](#common-commands) · [Troubleshooting](#troubleshooting)

</div>

---

## Prerequisites

Install the following before starting:

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Git](https://git-scm.com/downloads)
- Docker Desktop on Windows or macOS
- Docker Engine with the Compose plugin on Linux

Docker must be running before DevSetup is started.

Run the setup commands from the repository root.

---

<a id="windows"></a>

## Windows

<details open>
<summary><strong>Windows PowerShell setup</strong></summary>

### 1. Run DevSetup

```powershell
dotnet run --project .\backend\tools\Nordiska.DevSetup\Nordiska.DevSetup.csproj -- .
```

DevSetup creates the required local secrets, starts PostgreSQL, applies migrations and configures database permissions.

Wait for:

```text
[SUCCESS] Local database setup completed.
```

### 2. Start the complete environment

```powershell
cd .\infra\v2

docker compose --project-name nordiska-v2 `
  --env-file .env `
  -f docker-compose.yml `
  -f docker-compose.override.yml `
  up -d --build
```

### 3. Verify the containers

```powershell
docker compose ps
```

The `api`, `reporting-worker` and `db` containers should be running.

Open the services at:

- Combined frontend and backend: `http://localhost:8080`

</details>

[Back to top](#local-development-setup)

---

<a id="macos"></a>

## macOS

<details>
<summary><strong>macOS Terminal setup</strong></summary>

The commands below work in both `zsh` and `bash`.

### 1. Run DevSetup

```shell
dotnet run --project ./backend/tools/Nordiska.DevSetup/Nordiska.DevSetup.csproj -- .
```

DevSetup creates the required local secrets, starts PostgreSQL, applies migrations and configures database permissions.

Wait for:

```text
[SUCCESS] Local database setup completed.
```

### 2. Start the complete environment

```shell
cd ./infra/v2

docker compose --project-name nordiska-v2 \
  --env-file .env \
  -f docker-compose.yml \
  -f docker-compose.override.yml \
  up -d --build
```

### 3. Verify the containers

```shell
docker compose ps
```

The `api`, `reporting-worker` and `db` containers should be running.

Open the services at:

- Combined frontend and backend: `http://localhost:8080`

> macOS and Linux use `\` for command continuation. PowerShell backticks do not work in `zsh` or `bash`.

</details>

[Back to top](#local-development-setup)

---

<a id="linux"></a>

## Linux

<details>
<summary><strong>Linux Terminal setup</strong></summary>

The commands below work in `bash` and compatible shells.

### 1. Run DevSetup

```shell
dotnet run --project ./backend/tools/Nordiska.DevSetup/Nordiska.DevSetup.csproj -- .
```

DevSetup creates the required local secrets, starts PostgreSQL, applies migrations and configures database permissions.

Wait for:

```text
[SUCCESS] Local database setup completed.
```

### 2. Start the complete environment

```shell
cd ./infra/v2

docker compose --project-name nordiska-v2 \
  --env-file .env \
  -f docker-compose.yml \
  -f docker-compose.override.yml \
  up -d --build
```

### 3. Verify the containers

```shell
docker compose ps
```

The `api`, `reporting-worker` and `db` containers should be running.

Open the services at:

- Combined frontend and backend: `http://localhost:8080`

</details>

[Back to top](#local-development-setup)

---

## Common commands

The standard local Docker environment keeps the existing combined frontend and backend container:

| Container | Address | Responsibility |
|---|---|---|
| `api` | `http://localhost:8080` | Combined React frontend and .NET backend API |
| `reporting-worker` | No public port | Native PDF generation and job processing |
| `db` | `localhost:5433` | PostgreSQL database |

### Local frontend with the Docker backend

After DevSetup has been run once, frontend developers can run this from the `frontend` directory:

```shell
npm run setup:local-backend
```

The command uses the additive `docker-compose.frontend-local.yml` configuration. It creates `frontend/.env.local` when needed and starts only `db`, the backend-only `api` on `http://localhost:5031`, and `reporting-worker`. It never overwrites an existing `.env.local`, does not start Vite and does not change the standard combined Docker environment on `http://localhost:8080`.

Start the frontend separately:

```shell
npm run dev
```

Run the remaining Docker commands below from `infra/v2`.

### Start the environment

```shell
docker compose up -d
```

### Stop the environment

```shell
docker compose down
```

This stops the containers without deleting the database or generated PDF files.

### Rebuild the environment

```shell
docker compose up -d --build
```

### Show container status

```shell
docker compose ps
```

### Follow API logs

```shell
docker compose logs -f api
```

### Follow frontend logs

```shell
docker compose logs -f frontend
```

### Follow Reporting Worker logs

```shell
docker compose logs -f reporting-worker
```

### Show the latest Reporting Worker logs

```shell
docker compose logs --tail=50 reporting-worker
```

[Back to top](#local-development-setup)

---

## When should DevSetup be run?

Run DevSetup:

- After cloning the repository for the first time
- When new database migrations are added
- When database permissions change
- When new required environment variables are added

For normal restarts, running `docker compose up -d` from `infra/v2` is enough.

---

## Troubleshooting

### Password authentication failed

Example:

```text
password authentication failed for user "nordiska_migrator"
```

This usually means that `infra/v2/.env` contains different passwords from an already initialized PostgreSQL Docker volume.

If the local database and generated PDF files can be deleted, reset the local environment from the repository root.

> **Warning:** The following command deletes the local PostgreSQL database and the `report_documents` Docker volume.

#### Windows PowerShell

```powershell
docker compose --project-name nordiska-v2 `
  --env-file infra/v2/.env `
  -f infra/v2/docker-compose.yml `
  -f infra/v2/docker-compose.override.yml `
  down -v
```

#### macOS and Linux

```shell
docker compose --project-name nordiska-v2 \
  --env-file infra/v2/.env \
  -f infra/v2/docker-compose.yml \
  -f infra/v2/docker-compose.override.yml \
  down -v
```

Run DevSetup again after the reset.

If the local data must be preserved, do not use `down -v`. Restore the matching `.env` values or update the PostgreSQL role passwords instead.

### Docker is unavailable

Confirm that Docker is running:

```shell
docker version
docker compose version
```

### Show DevSetup errors

Run DevSetup again from the repository root and share the complete output:

```shell
dotnet run --project backend/tools/Nordiska.DevSetup/Nordiska.DevSetup.csproj -- .
```

---

<div align="center">

After the first successful setup, normal development only requires:

```shell
cd infra/v2
docker compose up -d
```

</div>
