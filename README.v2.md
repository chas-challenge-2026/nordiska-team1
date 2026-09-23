# Nordiska Sparbanken v2 - Kundportal & API

Välkommen till version 2 av Nordiska Sparbankens kundportal. Denna version ersätter den tidigare sårbara v1-monoliten med en modern, säker och skalbar arkitektur baserad på **.NET 8 Web API**, **Entity Framework Core 8** och en **React 18 SPA**.

---

## Snabbstart (Docker)

Hela systemet inklusive databas och frontend kan startas med Docker Compose:

```bash
cd infra
docker compose up --build
```

När containrarna har startat:
- **Kundportal (React SPA):** [http://localhost:8080](http://localhost:8080)
- **Interaktiv API-dokumentation (Scalar):** [http://localhost:8080/scalar/v1](http://localhost:8080/scalar/v1)
- **OpenAPI Schema (JSON):** [http://localhost:8080/openapi/v1.json](http://localhost:8080/openapi/v1.json)
- **Hälsokontroll:** [http://localhost:8080/health/database](http://localhost:8080/health/database)

---

## Testkonton och Autentisering

Systemet har två parallella inloggningsmetoder:

### 1. BankID (Rekommenderat)
I utvecklings- och testmiljö används ActiveLogin BankID Mock:
- Du kan logga in med **vilket 12-siffrigt personnummer som helst** (t.ex. `199001011234`).
- Om användaren inte finns i databasen skapas den dynamiskt tillsammans med ett primärt sparkonto och startkapital.

### 2. E-post & Lösenord
Fördefinierade testanvändare som seedas automatiskt vid uppstart:

| E-post | Lösenord | Personnummer |
|---|---|---|
| `anna@example.com` | `password123` | `198505051234` |
| `erik@example.com` | `password123` | `199001015678` |

### Säkerhetsarkitektur för inloggning
- **JWT i HttpOnly Cookie:** Vid lyckad inloggning genereras en signerad JWT som sätts i en säker, `HttpOnly` och `SameSite=Strict` cookie (`nordiska_auth_token`). Detta eliminerar risken för att tokens stjäls via XSS.
- **Lösenordshashning:** Lösenord hashas med ASP.NET Core Identitys säkra PBKDF2-implementering (MD5 från v1 har avlägsnats helt).

---

## Projektstruktur

Lösningen är uppbyggd som en **modulär monolit**:

```
repo/
├── backend/
│   ├── src/
│   │   ├── Nordiska.FrontendApi/       # HTTP API Gateway, Controllers, OpenAPI/Scalar, Auth
│   │   ├── Modules/
│   │   │   ├── Banking/                # Konton, transaktioner, saldon, kunder, BankID
│   │   │   ├── Faq/                    # FAQ-sökning och administration
│   │   │   └── Reporting/              # Skatteunderlag och rapportering
│   │   ├── BuildingBlocks/             # Delad infrastruktur, felhantering, databaskonfig
│   │   └── Nordiska.Reporting.Worker/  # Bakgrundsjobb för asynkron bearbetning
│   └── tests/                          # Enhets- och integrationstester
├── frontend/                           # React 18 SPA (TypeScript + Vite)
├── infra/                              # Docker Compose och databas-initiering
└── docs/                               # Arkitektur- och designdokumentation
```

---

## Köra lokalt för utveckling

### Förutsättningar
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/)
- [PostgreSQL 15](https://www.postgresql.org/) (eller via Docker)

### 1. Starta databasen
```bash
cd infra
docker compose up db -d
```

### 2. Starta Backend API
```bash
cd backend/src/Nordiska.FrontendApi
dotnet run
```
API:et startar på `http://localhost:5000` / `https://localhost:5001`.

### 3. Starta Frontend (React)
```bash
cd frontend
npm install
npm run dev
```
Frontend startar på `http://localhost:5173` med proxy till backend.

---

## Tester och Kvalitetssäkring

För att köra alla enhets- och integrationstester i lösningen:

```bash
dotnet test backend/np.sln
```

För att bygga lösningen och validera att inga kompileringsvarningar finns:

```bash
dotnet build backend/np.sln --warnaserror
```

---

## Säkerhets- och funktionsförbättringar jämfört med v1

| Område | Version 1 (Sårbar monolit) | Version 2 (Modern lösning) |
|---|---|---|
| **Arkitektur** | Razor Pages med inline SQL i sidmodeller | Ren REST API + React SPA med modulär uppdelning |
| **Autentisering** | MD5-hasher och årslång session i minnet | BankID + PBKDF2, JWT i HttpOnly SameSite=Strict cookies |
| **Dataåtkomst** | Rå ADO.NET med hårdkodade SQL-strängar | Entity Framework Core 8 med automatiserade migrationer |
| **Transaktionsintegritet** | Race conditions vid parallella överföringar | Ledger-mönster (append-only) med transaktionslåsning |
| **Felhantering** | Tysta `try-catch` som döljer fel | RFC 7807 `ProblemDetails` och strukturerad loggning |
| **API-dokumentation** | Saknades helt | Fullständig OpenAPI v3-specifikation och Scalar UI |
