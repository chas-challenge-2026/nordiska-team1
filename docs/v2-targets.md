# v2 Targets — What Students Should Build

v2 replaces the Razor Pages monolith with a proper layered system. Each component has a defined responsibility and a clear interface.

---

## Backend: ASP.NET Core Web API

- **Framework:** .NET 8, ASP.NET Core Web API (not Razor Pages)
- **ORM:** Entity Framework Core 8 with Code-First migrations
- **Pattern:** Repository + Service layer. PageModels become Controllers → Services → Repositories → DbContext.
- **Database:** PostgreSQL 15 (upgrade from 12). EF Core migrations replace `seed.sql`.
- **Ledger pattern for transactions:** Never mutate `balance` directly. Instead, append an immutable `LedgerEntry` row and compute balance as `SUM(amount)`. Eliminates the race condition structurally.
- **Auth:** Replace MD5 session cookie with JWT (short-lived access token + refresh token). Use `BCrypt.Net-Next` for password hashing. Optionally integrate BankID mock for v2.2.
- **Rate limiting:** `Microsoft.AspNetCore.RateLimiting` on sensitive endpoints: limit login and deposit/withdrawal to 10 req/min per customer.
- **Validation:** FluentValidation. No inline `if (amount <= 0)` in controllers.
- **Error handling:** Global `IExceptionHandler` middleware with structured logging (Serilog) and correlation IDs.
- **Health checks:** `/health` endpoint via `Microsoft.Extensions.Diagnostics.HealthChecks`.
- **Background jobs:** Hangfire or `IHostedService` for:
  - Year-end interest accrual
  - Batch PDF generation (delegates to native module)
  - Session cleanup

---

## FAQ Search

- **Rule-based search against a controlled FAQ database with correct match logic.** Replaces the hardcoded keyword list in `Pages/Faq.cshtml.cs`.
- **Model:** `FaqEntry` table (fraga, svar, kategori, nyckelord). Seeded and maintained via migrations, not source code.
- **Match logic:** normalize the question (lowercase, strip punctuation, basic stemming), score every entry against its keyword list and return the best-ranked hit, not the first one. Below a confidence threshold, return a "no answer" response with a customer service fallback.
- **API:** `GET /api/faq/search?q=...` served by a `FaqService`; the SPA renders the FAQ UI.

---

## Notifications

- **Notification handling as a proper component.** No email sending inline from controllers (v1 calls `SmtpClient` directly in the Deposit page handler).
- **Model:** `Notification` table (recipient, type, ref_id, status, sent_at).
- **Flow:** the transaction service writes a `Notification` row and returns immediately. A background worker (Hangfire or `IHostedService`) picks up pending rows, sends the email, retries with backoff on failure and updates `status`/`sent_at`. Request latency is never coupled to SMTP.

---

## Frontend: React 18 SPA

- **Framework:** React 18 + TypeScript + Vite
- **State:** Zustand or React Query for server state
- **UI:** Tailwind CSS (replace Bootstrap 3)
- **Auth:** Store JWT in memory (not localStorage). Refresh via HttpOnly cookie.
- **Key screens:**
  - Login (email/password → JWT)
  - Dashboard (account list, balance chart with Recharts)
  - Deposit/Withdrawal form (real-time balance preview)
  - Transaction history (virtualized list)
  - Tax report download (polling job status)
  - FAQ search (rule-based search against the FaqEntry database)

---

## Native Modules (C/C++)

See `native/README.md` for full spec.

### pdf_generator (C++)
- Batch-generate real PDFs using libharu or libpoppler
- Called from .NET background job via `Process.Start` or P/Invoke
- Target: 10,000 PDFs in < 5 minutes

### pdf_signer (C)
- SHA-256 + PKCS#1 signing via OpenSSL
- Embeds signature in PDF metadata for audit trail
- PKCS#11 HSM integration path

---

## Infrastructure

- **Docker Compose:** Add Redis (session store), Hangfire dashboard container
- **Reverse proxy:** Nginx in front of the app container (TLS termination)
- **Secrets:** Move all credentials to environment variables; document Docker Secrets or Vault integration
- **CI:** GitHub Actions pipeline: `dotnet test`, `npm test`, `cmake --build`, Docker build

---

## Definition of Done for v2

| Area | Requirement |
|------|-------------|
| Security | BCrypt passwords, JWT auth, no credentials in source |
| Data integrity | Ledger pattern, no race condition, EF Core transactions |
| Performance | PDF generation async + native, no Thread.Sleep in request path |
| Observability | Structured logs, correlation IDs, /health endpoint |
| Tests | >70% line coverage on service layer, integration tests for API endpoints |
| Docs | Updated architecture.md, ADR for key decisions |
