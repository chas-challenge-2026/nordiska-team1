# Nordiska Sparbanken — Koduppgift

Detta repo innehåller v1 av Nordiska Sparbankens kundportal. Koden är **avsiktligt skriven som spaghetti** — det är en pedagogisk utgångspunkt. Din uppgift är att refaktorera den till v2.

## Snabbstart

```bash
git clone <repo-url>
cd ChasChallenge/infra
docker compose up
```

Öppna [http://localhost:8080](http://localhost:8080) i webbläsaren.

**Testinloggning:**
| E-post | Lösenord |
|--------|----------|
| anna@example.com | password123 |
| erik@example.com | password123 |

## Vad som fungerar i v1

- Inloggning med e-post och lösenord
- Dashboard visar saldo och beräknad årsränta per konto
- Insättning och uttag (fungerar korrekt vid en användare i taget)
- Mailbekräftelse vid insättning och uttag (levereras bara om en SMTP-server finns, fel syns aldrig)
- Nedladdning av skatteunderlag som fil
- FAQ-sidan svarar på vanliga frågor (enkel nyckelordsmatchning)
- Utloggning

## Vad som inte fungerar

- **Parallella insättningar korrupterar saldo** — race condition, ingen transaktion eller radlåsning
- **Skatteunderlag tar lång tid** — `Thread.Sleep(50)` per transaktion blockerar request-tråden
- **MD5-lösenord** — kryptografiskt brutet, enkelt att knäcka med rainbow tables
- **Session löper ut om ett år** — utloggning rensar inte serversidans session
- **FAQ-assistenten svarar ofta fel** - första nyckelordsträffen vinner, ingen rankning, ingen normalisering utöver gemener
- **Mail skickas inline från sidhanteraren** - `SmtpClient` direkt i Deposit-handlern, ingen kö, ingen retry, fel sväljs tyst
- **Inga felloggar** — alla undantag sväljs, ingen spårbarhet
- **Inloggningsuppgifter i källkoden** — connectionstring med lösenord i `appsettings.json` och hårdkodad fallback i varje fil

## Vad ska ni bygga

Se [docs/v2-targets.md](docs/v2-targets.md) för fullständig specifikation.

Kortversion:
- .NET 8 Web API + EF Core 8 + ledger-mönster (ersätter direkta balance-uppdateringar)
- React 18 SPA (ersätter Razor Pages)
- BCrypt-lösenord + JWT-autentisering
- Bakgrundsjobb för PDF-generering
- Regelstyrd FAQ-sökning mot kontrollerad FAQ-databas med korrekt träfflogik
- Notifieringar som egen komponent med kö och retry
- Rate limiting på känsliga endpoints
- Native C/C++-moduler för batch-PDF och PDF-signering
- Strukturerad loggning och /health-endpoint

## Datamodell (ER-diagram v2)

Följande datamodell och entitetsrelationer gäller för v2 av Nordiska Sparbanken:

```mermaid
erDiagram
    Customer {
        bigint id PK
        string personal_num UK
        string name
        string email UK
        string phone_number "kontaktuppgift"
        string password_hash
        datetime created_at
    }

    AccountTypeConfig {
        string account_type PK "saving, debit etc"
        decimal interest_rate "t.ex. 1.45 eller 3.46"
        string description
    }

    SavingsAccount {
        bigint id PK
        bigint customer_id FK
        string account_number UK
        string account_type FK
        decimal balance "Ledger snapshot"
        decimal interest_rate
        string status "active, closed"
        datetime created_at
    }

    Transaction {
        bigint id PK
        bigint account_id FK
        string type "deposit, withdrawal, interest"
        decimal amount
        datetime created_at
    }

    TaxReport {
        bigint id PK
        bigint account_id FK
        int year
        string status "pending, generated, signed"
        string download_url
        string signature
        datetime created_at
    }

    FaqEntry {
        int id PK
        string question
        string answer
        string category
        int helpful_count
        string keywords "taggar eller sokord"
    }

    Notification {
        bigint id PK
        string recipient "email eller userId"
        string type "email, push, sms"
        bigint ref_id "FK till relaterad entitet"
        string status "pending, sent, failed"
        datetime sent_at
        datetime created_at
    }

    AuditEntry {
        bigint id PK
        string action "LOGIN, TRANSFER, UPDATE"
        bigint user_id FK "Nullable"
        string details "JSON eller text"
        string signature
        datetime created_at
    }

    Customer ||--o{ SavingsAccount : "owns"
    AccountTypeConfig ||--o{ SavingsAccount : "defines_rate_for"
    SavingsAccount ||--o{ Transaction : "has ledger entries"
    SavingsAccount ||--o{ TaxReport : "has"
    Customer ||--o{ AuditEntry : "logs"
```

## Dokumentation

| Fil | Innehåll |
|-----|----------|
| [docs/architecture.md](docs/architecture.md) | v1-arkitektur, databasschema, sekvensdiagram |
| [docs/known-bugs.md](docs/known-bugs.md) | Alla avsiktliga buggar förklarade med korrekta lösningar |
| [docs/README-pain-points.md](docs/README-pain-points.md) | Vad som fungerar, vad som inte fungerar, var v2 bör börja |
| [docs/v2-targets.md](docs/v2-targets.md) | Fullständig kravspec för v2 |
| [native/README.md](native/README.md) | Spec för native C/C++-moduler |

## Mappstruktur

```
ChasChallenge/
  backend/NordiskaPortal/   .NET 6 Razor Pages (v1 monolith)
  frontend/                 (tom — Razor Pages är frontendet i v1)
  native/                   (tom — se native/README.md för v2-spec)
  infra/
    docker-compose.yml      PostgreSQL 12 + app-container
    seed.sql                Databasschema och testdata
  docs/                     Arkitektur, kända buggar, v2-mål
```
