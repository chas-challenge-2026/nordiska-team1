# v1 Pain Points — What Works and What Doesn't

## What Works

- **Login flow:** You can log in with `anna@example.com` / `password123` and reach the dashboard.
- **Balance display:** The dashboard correctly shows account balances and calculates yearly interest (inline formula).
- **Deposit/withdrawal:** Single-user deposits and withdrawals work correctly when no concurrent requests are made.
- **Tax report:** Generates and downloads a text file with transaction history. Works for small accounts.
- **CSRF protection:** AntiForgery tokens are present and validated on all POST forms.
- **Docker Compose:** `docker compose up` from `infra/` starts the database and app together.
- **Session isolation:** Each browser session is independent.

## What Doesn't Work

### At Scale
- **Tax report generation times out** with more than ~200 transactions due to `Thread.Sleep(50)` per transaction in the request thread.
- **Concurrent deposits corrupt balance.** Open two browser tabs, submit a deposit of 1,000 kr from both simultaneously — one will be lost.

### Security
- **MD5 passwords** are trivially reversible. The seed data hash `482c811da5d5b4bc6d497ffa98491e38` corresponds to `password123` — easily verified on any rainbow table site.
- **Session tokens last 365 days.** Logging out does not invalidate the server-side session.
- **Credentials in source code.** `appsettings.json` and the `FALLBACK_CONN` constants contain the live database password.

### Observability
- **No logs.** All exceptions are caught and discarded. If the DB goes down, no alert fires.
- **No health endpoint.** No `/health` or `/ready` route. Load balancer cannot detect a broken instance.
- **No correlation IDs.** Impossible to trace a user's request across logs.

### Maintainability
- **Business logic is in the view layer.** Interest calculation, tax rate, withdrawal limits — all hardcoded in PageModel methods.
- **No tests.** There is no test project. No unit tests, no integration tests.
- **Magic numbers everywhere.** `0.30m`, `50000m`, `365`, `50` (Thread.Sleep) — none documented, none configurable.
- **Duplicate code.** Every PageModel opens its own connection, duplicates connection-string resolution, and duplicates error-swallowing patterns.

## Where v2 Should Focus First

1. **Fix the race condition** — this is a data integrity bug, not just a smell.
2. **Replace MD5** — security vulnerability affecting real user data.
3. **Add proper logging** — prerequisite for everything else.
4. **Extract data access** — repository pattern or EF Core makes everything else testable.
5. **Move PDF to background job** — unblocks the thread pool.
