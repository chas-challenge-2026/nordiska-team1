# Quickstart Setup Guide

Run all commands starting from the repository root (`nordiska-team1`).

---

## 1. Local Initialization (DevSetup)

Generates secrets into `infra/.env`, boots the database, runs migrations, and applies permissions:

```bash
dotnet run --project ./backend/tools/Nordiska.DevSetup/Nordiska.DevSetup.csproj -- .
```

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

## 4. Teardown & Reset

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

