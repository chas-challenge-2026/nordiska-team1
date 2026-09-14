# Docker Quickstart and Run Guide: Nordiska Sparbanken v2

This guide is for everyone on the team (whether you are running Windows, macOS, or Linux). Using Docker, you can start the entire system (PostgreSQL 15 database, .NET 8 Web API, and React SPA) with a single command without needing to install Node, .NET SDK, or PostgreSQL locally.

---

## 1. Quickstart: Start the System

### Prerequisites
* Docker Desktop (Windows/Mac) or Docker Engine (Linux) must be installed and running.
* **If you previously ran `DevSetup` / `infra/v2`:** Stop the existing database container first to free port 5433:
  ```bash
  docker compose -f infra/v2/docker-compose.yml down
  ```

### Start Everything
Open a terminal in the repository root and run:

```bash
cd infra
docker compose up --build
```

Once the containers are built and started:
* Landing Page: [http://localhost:8080/welcome](http://localhost:8080/welcome)
* Login Page: [http://localhost:8080/login](http://localhost:8080/login)
* Scalar API Documentation: [http://localhost:8080/scalar/v1](http://localhost:8080/scalar/v1)

---

## 2. Test Login Credentials (Demo Users)

| Email | Password | Role |
| :--- | :--- | :--- |
| `anna@example.com` | `password123` | Customer (Anna Lindqvist) |
| `erik@example.com` | `password123` | Customer (Erik Johansson) |

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
