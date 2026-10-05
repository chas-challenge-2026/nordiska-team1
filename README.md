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

> `password123` fungerar bara i Development och bara för seedade konton utan lösenord. Efter 5 felaktiga försök spärras kontot i 15 minuter (BankID-inloggning häver spärren).

| BankID |
|--------|
| 197903142380 |
| 198202116050 |

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
        string personal_num UK "BankID personnummer"
        string name "Fullständigt namn"
        string email UK "E-postadress"
        string phone_number "Kontaktuppgift"
        string password_hash "Lösenordshash"
        datetime created_at "Skapad tidpunkt"
        datetime updated_at "Senast uppdaterad"
    }

    AccountTypeConfig {
        string account_type PK "saving checking flex"
        decimal interest_rate "Räntesats"
        string description "Beskrivning"
    }

    AccountTypeRateHistory {
        bigint id PK
        string account_type FK "saving flex fix standard premium"
        decimal interest_rate "Gällande räntesats för perioden"
        datetime effective_from_utc "Giltig från och med"
        datetime effective_to_utc "Giltig till och med"
        datetime created_at_utc "Skapad tidpunkt"
    }

    SavingsAccount {
        bigint id PK
        bigint customer_id FK
        string account_number UK "NOR-XXXXXX autogenererad"
        string account_name "Valbart kontonamn"
        string account_type FK "saving checking flex"
        decimal balance "Ledger snapshot"
        decimal interest_rate "Aktuell ränta"
        string status "active closed"
        datetime created_at "Skapad tidpunkt"
        datetime updated_at "Senast ändrad"
    }

    Transaction {
        bigint id PK
        bigint account_id FK
        string type "deposit withdrawal transfer"
        decimal amount "Belopp"
        string label "Etikett"
        bigint target_account_id "Motpartskonto vid transfer"
        boolean is_planned "Planerad framtida transaktion"
        datetime planned_date "Planerat datum"
        string repeating "week month year"
        datetime created_at "Skapad tidpunkt"
    }

    TaxReport {
        bigint id PK
        bigint account_id FK
        int year "Skatteunderlagsår"
        string status "pending generated signed"
        string download_url "Url till PDF"
        string signature "Digital signatur"
        datetime created_at "Skapad tidpunkt"
    }

    FaqEntry {
        int id PK
        uuid relation_id "Koppling för språkpar sv och en"
        string language "Språkkod sv eller en"
        string question "Fråga"
        string answer "Svar"
        string category "Kategori"
        int helpful_count "Antal gillningar"
        string keywords "Taggar och sökord"
        int_array related_faq_ids "Relaterade artiklar"
        datetime created_at "Skapad tidpunkt"
        datetime updated_at "Senast uppdaterad"
    }

    Notification {
        bigint id PK
        string recipient "E-post eller userId"
        string type "email push sms"
        bigint ref_id "FK till relaterad entitet"
        string status "pending sent failed"
        datetime sent_at "Skickat tidpunkt"
        datetime created_at "Skapad tidpunkt"
    }

    AuditEntry {
        bigint id PK
        string action "LOGIN TRANSFER UPDATE"
        bigint user_id FK "Användar-ID"
        string details "JSON eller text"
        string signature "Signatur"
        datetime created_at "Skapad tidpunkt"
    }

    MessageBox {
        bigint id PK
        bigint customer_id FK "Kopplad till kund"
        int type "Personal"
        datetime created_at "Skapad tidpunkt"
    }

    MessageThread {
        bigint id PK
        bigint message_box_id FK
        string subject "Ärendeämne"
        int status "Open Closed"
        datetime created_at "Skapad tidpunkt"
        datetime last_message_at "Senaste meddelande"
    }

    Message {
        bigint id PK
        bigint thread_id FK
        int sender_type "Bank Customer System"
        bigint sender_customer_id FK "Avsändar-ID"
        string body "Meddelandetext"
        boolean reply_allowed "Om svar är tillåtet"
        datetime sent_at "Skickat tidpunkt"
        datetime revoked_at "Återkallat tidpunkt"
    }

    MessageThreadState {
        bigint id PK
        bigint thread_id FK
        bigint customer_id FK
        int folder "Inbox Sent Archive"
        datetime read_at "Läst tidpunkt"
        datetime archived_at "Arkiverat tidpunkt"
    }

    CustomerNotification {
        bigint id PK
        bigint customer_id FK
        string type "Notistyp"
        int priority "Low Normal High Critical"
        string title "Rubrik"
        string body "Notistext"
        int target_type "Document MessageThread Term"
        bigint target_id "Mål-ID"
        datetime created_at "Skapad tidpunkt"
        datetime read_at "Läst tidpunkt"
        datetime expires_at "Utgångsdatum"
    }

    FeedItem {
        bigint id PK
        bigint customer_id FK
        string type "Händelsetyp"
        string title "Rubrik"
        string body "Text"
        datetime published_at "Publicerat tidpunkt"
    }

    Document {
        bigint id PK
        string document_type "Dokumenttyp"
        string title "Dokumenttitel"
        string content_type "MIME-typ"
        string storage_path "Lagringssökväg"
        datetime created_at "Skapad tidpunkt"
    }

    CustomerDocument {
        bigint id PK
        bigint document_id FK
        bigint customer_id FK
        datetime published_at "Publicerat tidpunkt"
        datetime first_opened_at "Först öppnad"
        datetime available_until "Tillgänglig till"
    }

    Term {
        bigint id PK
        string code "Villkorskod"
        int version "Versionsnummer"
        string title "Villkorsrubrik"
        bigint document_id FK
        int status "Draft Active Superseded Archived"
        datetime effective_from "Gäller från"
    }

    TermAcceptance {
        bigint id PK
        bigint term_id FK
        bigint customer_id FK
        int status "Pending Accepted Declined"
        datetime created_at "Skapad tidpunkt"
        datetime accepted_at "Godkänt tidpunkt"
        datetime declined_at "Avböjt tidpunkt"
    }

    Customer ||--o{ SavingsAccount : "owns"
    AccountTypeConfig ||--o{ SavingsAccount : "defines_rate_for"
    AccountTypeConfig ||--o{ AccountTypeRateHistory : "has_rate_history"
    SavingsAccount ||--o{ Transaction : "has ledger entries"
    SavingsAccount ||--o{ TaxReport : "has"
    Customer ||--o{ AuditEntry : "logs"
    Customer ||--|o MessageBox : "has"
    MessageBox ||--o{ MessageThread : "contains"
    MessageThread ||--o{ Message : "contains"
    MessageThread ||--o{ MessageThreadState : "tracks"
    Customer ||--o{ MessageThreadState : "maintains"
    Customer ||--o{ CustomerNotification : "receives"
    Customer ||--o{ FeedItem : "receives"
    Customer ||--o{ CustomerDocument : "owns"
    Document ||--o{ CustomerDocument : "distributed_as"
    Document ||--o{ Term : "defines"
    Term ||--o{ TermAcceptance : "accepted_through"
    Customer ||--o{ TermAcceptance : "signs"
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
