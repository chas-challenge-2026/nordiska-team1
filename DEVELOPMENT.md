# Development & Operational Guide

This document provides developer workflows, environment setup, local testing, and diagnostic commands for the **Nordiska Sparbanken** platform.

---

## Prerequisites

- **Git** & **Docker Engine 24.0+** with Docker Compose
- **.NET 8 SDK** (minimum version `8.0.400` as specified in [backend/global.json](backend/global.json))
- **Node.js 20+** and `npm` (matching version constraints in [frontend/package-lock.json](frontend/package-lock.json))
- *(Optional for direct native compilation)*: Linux x86-64, C++23 compiler (GCC 13+ or Clang 17+), CMake 3.25+, Ninja, pkg-config, ZLIB, OpenSSL 3.0+, and Cairo (`libcairo2-dev`). The Docker build compiles native components automatically in its own toolchain.

---

## 1. Quick Start (Full Stack via Docker)

### Step 1: Initialize Database & Environment Secrets
Run the bootstrap setup tool from repository root:

```bash
# macOS / Linux
dotnet run --project ./backend/tools/Nordiska.DevSetup/Nordiska.DevSetup.csproj -- .
```

```powershell
# Windows PowerShell
dotnet run --project .\backend\tools\Nordiska.DevSetup\Nordiska.DevSetup.csproj -- .
```

> [!NOTE]
> `DevSetup` generates local development secrets in `infra/.env`, initializes PostgreSQL, runs Entity Framework Core migrations, and establishes role-based access control. See [DevSetup README](backend/tools/Nordiska.DevSetup/README.md).

### Step 2: Launch Stack
```bash
cd infra
docker compose up -d --build
cd ..
```

### Step 3: Access Points
- **Customer & Admin Portal:** [http://localhost:8080](http://localhost:8080)
- **Backend API Docs (Scalar):** [http://localhost:8080/scalar/v1](http://localhost:8080/scalar/v1)
- **Health Checks:** [http://localhost:8080/health](http://localhost:8080/health)

> [!NOTE]
> The local Docker Compose environment runs in development mode with simulated BankID authentication. Successful local login verifies the auth workflow, not live production BankID integration.

---

## 2. Frontend Development with Hot-Reload (Vite HMR)

For rapid UI development with instant hot-reloading:

```bash
cd frontend
npm ci
npm run setup:local-backend   # Starts local backend API on port 5031
npm run dev                    # Vite development server on port 5173
```

| Service | Local URL |
| :--- | :--- |
| **Frontend (Vite HMR)** | [http://localhost:5173](http://localhost:5173) |
| **Backend API** | [http://localhost:5031](http://localhost:5031) |
| **Scalar API Documentation** | [http://localhost:5031/scalar/v1](http://localhost:5031/scalar/v1) |

---

## 3. Synthetic Database & Test Data Seeding

Generate varied test cohorts (customers, multi-currency accounts, transactions, and historical data) to test UI interactions and stress reporting pipelines:

```bash
# Interactive menu (Linux/WSL)
./tools/syntheticDB/nordiska-synthetic-linux-x86_64
```

### Common CLI Options:
```bash
# Seed 10,000 customers with accounts and transactions
./tools/syntheticDB/nordiska-synthetic-linux-x86_64 --customers 10000

# Switch current active database to synthetic
./tools/syntheticDB/nordiska-synthetic-linux-x86_64 --switch nordiska_synthetic

# Output credentials and details for a random test customer
./tools/syntheticDB/nordiska-synthetic-linux-x86_64 --get-random-customer
```

For platform limitations and database cleanup rules, see [Synthetic DB Documentation](tools/syntheticDB/README.md).

---

## 4. Performance Benchmarking (TUI & CLI)

The reporting benchmark evaluates end-to-end batch generation across database staging, worker polling, C++23 PDF generation, PKCS#11 signing, and storage.

```bash
# Launch interactive TUI
./tools/benchmark/bench-tui.sh
```

> [!IMPORTANT]
> The benchmark utility resets test report tables prior to execution. Always verify that you are connected to a dedicated test or synthetic database before launching runs.

- Guide & Usage: [tools/benchmark/README.md](tools/benchmark/README.md)

---

## 5. Building Native PDF Generator Locally

To build and run the C++23 PDF generator outside Docker:

```bash
cd native/pdf_generator
cmake --preset debug
cmake --build --preset debug -j
ctest --preset debug
```

- Produces CLI executable: `build/debug/pdf_generator`
- Produces shared C ABI library: `build/debug/libnordiska_pdf_generator_c_api.so`
- For production builds, substitute `release` for `debug`. See [Native PDF Generator README](native/pdf_generator/README.md).

---

## 6. Teardown & Environment Reset

### Safe Stop (Preserves Database Volumes)
```bash
cd infra
docker compose down
cd ..
```

### Full Reset (Removes Volumes and Local Images)
> [!WARNING]
> This command irreversibly removes local database volumes.

```bash
cd infra
docker compose down -v --rmi local
cd ..
```

---

## 7. Troubleshooting

### WSL / Ubuntu: .NET SDK `CS0121` Ambiguous Invocation
If `dotnet run` fails with `CS0121: The call is ambiguous...` around collection expressions, your .NET 8 SDK is outdated. Verify `dotnet --version` against `backend/global.json` (requires 8.0.400+).

Update .NET SDK:
```bash
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"
dotnet --version
```

