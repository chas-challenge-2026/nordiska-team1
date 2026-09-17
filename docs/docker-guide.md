# Docker Quickstart and Run Guide: Nordiska Sparbanken v2

This guide is for everyone on the team (whether you are running Windows, macOS, or Linux). Using Docker, you can start the entire system (PostgreSQL 15 database, .NET 8 Web API, and React SPA) with a single command without needing to install Node, .NET SDK, or PostgreSQL locally.

---

## 1. Quickstart: Choose Your Workflow

You can run the application in two ways depending on your needs:

---

### Workflow A: Local Development with Hot-Reload (Recommended for active coding)

In this mode, PostgreSQL runs inside Docker while Backend and Frontend run locally with hot-reloading.

#### 1. Start PostgreSQL Database
```bash
cd infra
docker compose up -d db
```

#### 2. Start Backend API (runs on `http://localhost:5031`)
Open a new terminal:
```bash
cd backend/src/Nordiska.FrontendApi
dotnet run
```
*Migrations and seed data are applied automatically.*

#### 3. Start Frontend React App (runs on `http://localhost:5173`)
Open another terminal:
```bash
cd frontend
npm install
npm run dev
```

* **Frontend:** [http://localhost:5173](http://localhost:5173)
* **Backend API:** [http://localhost:5031](http://localhost:5031)
* **Scalar Docs:** [http://localhost:5031/scalar/v1](http://localhost:5031/scalar/v1)

---

### Workflow B: Full Docker (Runs everything in containers)

In this mode, everything (DB, Backend, Frontend, Reverse Proxy) is packaged into containers. No local .NET or Node installation is required.

```bash
cd infra
docker compose up --build
```

* **Frontend:** [http://localhost:8080](http://localhost:8080)
* **Login:** [http://localhost:8080/login](http://localhost:8080/login)
* **Transactions:** [http://localhost:8080/transactions](http://localhost:8080/transactions)
* **Transfer:** [http://localhost:8080/transfer](http://localhost:8080/transfer)
* **Settings:** [http://localhost:8080/settings](http://localhost:8080/settings)
* **Scalar API Docs:** [http://localhost:8080/scalar/v1](http://localhost:8080/scalar/v1)

---

## 2. Test Accounts and Authentication

The portal supports two parallel authentication methods:

### A. Password Login (Email & Password)
Seeded test accounts available out of the box:

| Email | Password | Personal Number | Name |
| :--- | :--- | :--- | :--- |
| `anna@example.com` | `password123` | `198202116050` | Anna Smith |
| `erik@example.com` | `password123` | `197903142380` | Erik Svensson |

> `password123` only works when `ASPNETCORE_ENVIRONMENT=Development` and only for seeded accounts without a password. After 5 failed attempts the account is locked for 15 minutes; a BankID login lifts the lockout.

### B. BankID Login (Dynamic Mock)
* You can log in using **any 12-digit Swedish personal number** (e.g. `199001011234`).
* If the user does not exist in the database, a customer profile and primary savings account with initial balance are created dynamically.

### C. Local Development Database Credentials (PostgreSQL)

| Property | Value |
| :--- | :--- |
| **Host** | `localhost` (or `db` inside Docker network) |
| **Port** | `5432` |
| **Database** | `nordiska_v2` |
| **Username** | `nordiska_migrator` |
| **Password** | `migrator_secret_123` |

### How to Authenticate via BankID Simulator (in Scalar):

1. Open Scalar: [http://localhost:8080/scalar/v1](http://localhost:8080/scalar/v1)
2. **Initiate:** Run `POST /api/auth/bankid/initiate` with `{"personalNum": "199908072391"}` and copy the returned `orderRef`.
3. **Collect:** Run `POST /api/auth/bankid/collect` with `{"orderRef": "<your_orderRef>"}`. Click **Send** 1-2 times until status is `COMPLETE`.
4. **Access Frontend:** Open [http://localhost:8080/transactions](http://localhost:8080/transactions) or [http://localhost:8080/transfer](http://localhost:8080/transfer) in the same browser.

---

## 3. Stop and Maintenance Commands

### Stop Containers
Press `Ctrl + C` in the terminal or run:
```bash
docker compose down
```

### Start in Background (Detached Mode)
```bash
docker compose up -d
```

### View Live Logs
```bash
docker compose logs -f
```

### Reset Database (Start Fresh)
```bash
docker compose down -v
```
```bash
docker compose up --build
```

---

## 4. Common Issues and Troubleshooting

<details>
<summary><b>Problem 1: Bind for 127.0.0.1:5433 failed: port is already allocated</b></summary>

* **Cause:** A previous PostgreSQL instance from `DevSetup` (`infra/v2`) or another container is already running on port 5433 (or 5432).
* **Solution:**
  1. If you ran `DevSetup` previously, run:
  ```bash
  docker compose -f infra/v2/docker-compose.yml down
  ```
  2. Or check and stop any remaining container:
  ```bash
  docker ps
  docker stop <CONTAINER_ID>
  ```
</details>

<br>

<details>
<summary><b>Problem 2: WSL 2 installation is incomplete (Windows)</b></summary>

* **Cause:** Docker Desktop on Windows requires WSL2 (Windows Subsystem for Linux).
* **Solution:**
  1. Open PowerShell as Administrator.
  2. Run the command:
  ```powershell
  wsl --install
  ```
  3. Restart your computer and start Docker Desktop again.
</details>

<br>

<details>
<summary><b>Problem 3: Code changes are not reflected in the browser</b></summary>

* **Cause:** Docker reuses the previously built image unless the `--build` flag is provided.
* **Solution:** Always run:
  ```bash
  docker compose up --build
  ```
</details>

<br>

<details>
<summary><b>Problem 4: Database enters an inconsistent state or tables are missing</b></summary>

* **Cause:** An old Docker volume exists with outdated or incomplete schema files.
* **Solution:** Clear the local database volume and rebuild from scratch:
  ```bash
  docker compose down -v
  ```
  ```bash
  docker compose up --build
  ```
  Automatic EF Core migrations will execute and recreate all tables directly upon startup.
</details>

<br>

<details>
<summary><b>Problem 5: Error: Cannot connect to the Docker daemon</b></summary>

* **Cause:** Docker Desktop is not currently running on your machine.
* **Solution:** Launch the Docker Desktop application and wait until the status bar shows "Engine running".
</details>
