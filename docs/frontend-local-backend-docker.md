# Starta frontend lokalt med backend i Docker

Backend, databas, Reporting Worker och native PDF-generator körs i Docker. Frontend kan samtidigt köras lokalt med `npm run dev`.

**Alla kommandon nedan körs från repository-roten!!!**

Backend kommer finnas på port **5031** så ni är beredda ifall ni måste ändra detta i er konfiguration!

---

## 1. Första setup

Detta behöver bara göras första gången.

### Windows PowerShell

```powershell
dotnet run --project .\backend\tools\Nordiska.DevSetup\Nordiska.DevSetup.csproj -- .
```

### macOS/Linux

```bash
dotnet run --project ./backend/tools/Nordiska.DevSetup/Nordiska.DevSetup.csproj -- .
```

### Konsolstatus ska vara

```text
[SUCCESS] Local database setup completed.
```

#### Troligtvis kommer ni få `password authentication failed` här. Kör i så fall steg 1.2. Funkar detta så kan ni gå vidare direkt till steg 2.

---

## 1.2 Reparera lösenord vid behov (`password authentication failed`)

### Windows PowerShell

```powershell
dotnet run --project .\backend\tools\Nordiska.DevSetup\Nordiska.DevSetup.csproj -- . repair-passwords
```

### macOS/Linux

```bash
dotnet run --project ./backend/tools/Nordiska.DevSetup/Nordiska.DevSetup.csproj -- . repair-passwords
```

Om kommandot avslutas med `[SUCCESS] Local database setup completed.` fortsätter ni direkt till steg 2. Ni behöver inte köra steg 1 igen.

---

## 2. Starta backend i Docker

Detta startar databasen, API:t och Reporting Worker. Native PDF-generatorn byggs in i Reporting Worker.

### Windows PowerShell

```powershell
docker compose --project-name nordiska-v2 `
  --env-file infra/v2/.env `
  -f infra/v2/docker-compose.yml `
  -f infra/v2/docker-compose.frontend-local.yml `
  up -d --build db api reporting-worker
```

### macOS/Linux

```bash
docker compose --project-name nordiska-v2 \
  --env-file infra/v2/.env \
  -f infra/v2/docker-compose.yml \
  -f infra/v2/docker-compose.frontend-local.yml \
  up -d --build db api reporting-worker
```

---

## 4. Bekräfta att containrarna körs

```shell
docker compose --project-name nordiska-v2 --env-file infra/v2/.env -f infra/v2/docker-compose.yml -f infra/v2/docker-compose.frontend-local.yml ps
```

Följande tre tjänster ska visas:

```text
db
api
reporting-worker
```

**Pasted image 20261008010734.png**

---

## Nu kan ni starta frontenden via npm som vanligt!

---

## Scalar API url

[http://localhost:5031/scalar/v1](http://localhost:5031/scalar/v1)
