# v1 Architecture — Nordiska Sparbanken Portal

## Overview

v1 is a Razor Pages monolith. There are no layers, no services, no repositories. Everything lives in the `PageModel` classes.

```
Browser
  │
  ▼
ASP.NET Core Razor Pages (NordiskaPortal)
  │  ├── Pages/Index          — Login (MD5 auth, raw SQL)
  │  ├── Pages/Dashboard      — Balance view (raw SQL)
  │  ├── Pages/Deposit        — Deposit/withdrawal (raw SQL, no transaction)
  │  ├── Pages/TaxReport      — Synchronous PDF generation (Thread.Sleep)
  │  └── Pages/Logout         — Session.Clear()
  │
  ▼
PostgreSQL 12 (via Npgsql, ADO.NET — no ORM)
  │  ├── customers            — id, name, email, password_md5
  │  ├── savings_accounts     — id, customer_id, account_number, balance, interest_rate
  │  └── transactions         — id, account_id, type, amount, balance_after, created_at
```

## Auth

- User submits email + password via HTTP POST form.
- Password is MD5-hashed in the `IndexModel` and compared to stored MD5 hash in DB.
- On success, `customer_id` and `customer_name` are written to an ASP.NET Core session cookie.
- Session is checked on each page via `HttpContext.Session.GetString("CustomerId") != null`.
- Session cookie never expires server-side (configured to 365 days).

## Data Access

Every page that needs data opens its own `NpgsqlConnection`, runs raw SQL, and closes it. No connection pooling configuration. No ORM. No repository pattern. Connection string is read from `appsettings.json` with a hardcoded fallback string in each file.

## PDF Generation

Tax reports are generated synchronously in the HTTP request pipeline:
1. All transactions for the selected year are fetched into memory.
2. Each transaction line is "rendered" with a `Thread.Sleep(50)` to simulate work.
3. The output is a UTF-8 text file with a `.pdf` extension (not a real PDF).
4. The file is returned as a `FileResult`.

For 3 transactions this is ~150ms overhead. For year-end batch (10,000 accounts × 50 transactions), this would require concurrent requests and would hit ASP.NET Core's request timeout.

## Configuration

`appsettings.json` contains the live database connection string including credentials. There is also a hardcoded fallback `const string FALLBACK_CONN` in each page model.

## Error Handling

All database calls are wrapped in `try { } catch { }` with no logging. Errors are silently swallowed. The user either sees a blank page, a redirect to the dashboard, or a generic error message.

## Deployment

Docker Compose: `db` (Postgres 12) + `app` (.NET 8 Razor Pages on port 8080). The app connects to `db` by hostname. No TLS. No reverse proxy in v1.
