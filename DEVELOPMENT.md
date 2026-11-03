# Quickstart Setup Guide

Run all commands starting from the repository root (`nordiska-team1`).

---

## 1. Local Initialization (DevSetup)

Generates secrets into `infra/.env`, boots the database, runs migrations, and applies permissions:

```bash
dotnet run --project ./backend/tools/Nordiska.DevSetup/Nordiska.DevSetup.csproj -- .
```

> [!TIP]
> **WSL / Ubuntu .NET SDK Requirement (`>= 8.0.400`):**  
> If `dotnet run` fails with `CS0121: The call is ambiguous...` on collection expressions, your system has an older .NET 8 SDK installed (Ubuntu 24.04 APT packages ship `8.0.1xx`).  
> Nordiska requires SDK version **8.0.400+** (as specified in `backend/global.json`). To install the latest SDK in WSL/Linux:
> ```bash
> curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0
> echo 'export DOTNET_ROOT=$HOME/.dotnet' >> ~/.bashrc
> echo 'export PATH=$DOTNET_ROOT:$PATH' >> ~/.bashrc
> source ~/.bashrc
> ```

---

## 2. Start Application (Choose One)

### Option A: Full Docker Stack (All Services in Docker)
Best for testing the entire system together:

```bash
cd infra
docker compose up -d --build
cd ..
```
* Web App available at: **http://localhost:8080**

### Option B: Frontend Hot-Reload Workflow (Local Dev)
Best for frontend work. Runs backend in Docker and Vite locally with hot-reloading:

```bash
cd frontend
npm install                      # First time only
npm run setup:local-backend      # Boots backend in Docker on port 5031
npm run dev                      # Starts Vite with instant hot-reload
```
* Frontend (Vite HMR): **http://localhost:5173**
* Backend API: **http://localhost:5031** (Scalar docs at `/scalar/v1`)

---

## 3. Synthetic Database

Switch to a separate database seeded with test accounts and transactions.

### Interactive Menu (Recommended)
```bash
./tools/syntheticDB/nordiska-synthetic-linux-x86_64
```
* Select **Create synthetic data** (e.g. 10k customers).
* Select **Switch to SYNTHETIC DB**.
* Select **Random customer login** to get test credentials (`password123`).

### CLI One-Liners
```bash
# 1. Generate 10,000 customers
./tools/syntheticDB/nordiska-synthetic-linux-x86_64 --customers 10000

# 2. Switch running stack to synthetic DB
./tools/syntheticDB/nordiska-synthetic-linux-x86_64 --switch nordiska_synthetic

# 3. Print test customer credentials for web login
./tools/syntheticDB/nordiska-synthetic-linux-x86_64 --get-random-customer

# 4. Switch back to normal database anytime:
./tools/syntheticDB/nordiska-synthetic-linux-x86_64 --switch nordiska_v2
```

---

## 4. Benchmarking Tool

Benchmark the end-to-end reporting pipeline (tax reports and account statements) against the production `.NET` worker container, evaluating job staging, DB queue draining (`SKIP LOCKED`), native Haru PDF generation, PKCS#11 socket signing, and storage persistence.

### Interactive TUI (Recommended)
An interactive terminal controller to configure worker settings and run benchmarks:
```bash
./tools/benchmark/bench-tui.sh
```
* **Signing Presets:** Toggle between Unsigned, Plain CMS, and Timestamped (RFC 3161 TSA).
* **Worker Concurrency:** Adjust worker batch consumer concurrency slots on the fly.
* **TSA Latency Simulation:** Configure simulated network latency and jitter.
* **Run Benchmark:** Trigger whole-database benchmark runs directly from the menu.

### CLI Usage
Run the standalone C++23 benchmark binary directly:
```bash
# Run full whole-database benchmark (auto-detects database from infra/.env)
./tools/benchmark/nordiska-benchmark

# Test run limited to first 50 customers
./tools/benchmark/nordiska-benchmark --limit 50

# Target a specific database with 1-second progress polling
./tools/benchmark/nordiska-benchmark --db nordiska_synthetic --poll 1
```
*(To rebuild the benchmark binary: `./tools/benchmark/build-linux-release.sh`)*

For architecture details and metrics, see [tools/benchmark/README.md](tools/benchmark/README.md). For scalability benchmarks and latency percentiles, see [docs/benchmark-report.md](docs/benchmark-report.md).

---

## 5. Teardown & Reset

Run from `infra/`:

* **Stop stack (preserves database data):**
  ```bash
  cd infra
  docker compose down
  ```

* **Full wipe (removes containers, networks, and database volumes):**
  ```bash
  cd infra
  docker compose down -v
  ```

* **Complete reset (also removes locally built images):**
  ```bash
  cd infra
  docker compose down -v --rmi local
  ```

