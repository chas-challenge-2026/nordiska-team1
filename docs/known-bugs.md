# Known Bugs — v1

These bugs are **intentional** for pedagogical purposes. Students should identify, understand, and fix them in v2.

---

## Bug 1: Race Condition on Balance Updates

**File:** `Pages/Deposit.cshtml.cs`

**What happens:** Two concurrent deposit requests both read the current balance, both compute the new balance independently, and both write back. The second write overwrites the first. Net result: one deposit is silently lost.

```sql
-- This runs without a transaction or row lock:
UPDATE savings_accounts SET balance = balance + @delta WHERE id = @id
```

**Correct fix:** Wrap the SELECT + UPDATE + INSERT in a single `BEGIN` / `COMMIT` block with `SELECT FOR UPDATE` on the account row, or use EF Core's optimistic concurrency with a rowversion column.

---

## Bug 2: Tax Report Blocks the Thread

**File:** `Pages/TaxReport.cshtml.cs`

**What happens:** `Thread.Sleep(50)` is called once per transaction inside the HTTP request handler. For a customer with 3 transactions this is 150ms. For year-end batch across thousands of accounts this will exhaust the thread pool and time out requests.

**Correct fix:** Move PDF generation to a background job (IHostedService, Hangfire, or similar). Use a native C/C++ PDF generator for actual throughput. Return a job ID to the client; poll or use SignalR for completion notification.

---

## Bug 3: MD5 Password Hashing

**File:** `Pages/Index.cshtml.cs`, `infra/seed.sql`

**What happens:** Passwords are hashed with MD5, which is cryptographically broken. MD5 hashes can be reversed via rainbow tables in seconds. The entire `customers` table is a credential dump waiting to happen.

**Correct fix:** Use `BCrypt.Net-Next` or `Microsoft.AspNetCore.Identity`'s `PasswordHasher<T>` (PBKDF2 with 350,000 iterations). Never store MD5.

---

## Bug 4: CSRF Protection — Partially Working

**Status:** AntiForgery tokens are included via `@Html.AntiForgeryToken()` in all forms and ASP.NET Core validates them by default on POST. This actually works correctly in v1.

**However:** The form action URLs are predictable, and combined with the session-forever bug (Bug 6), a CSRF token stolen from an active session would remain valid for a year.

---

## Bug 5: No Withdrawal Limit Enforcement Beyond One Hardcoded Check

**File:** `Pages/Deposit.cshtml.cs`

**What happens:** `MAX_WITHDRAWAL = 50000m` is checked in the handler, but:
- There is no daily aggregate limit (10 × 49,999 kr/day = 499,990 kr unlimited).
- There is no check that the account actually belongs to the authenticated customer before loading it in `LoadAccounts` — though the WHERE clause does include `customer_id`, the `accountId` from the POST is trusted directly.
- No audit alert is triggered for large withdrawals.

---

## Bug 6: Session Never Expires Server-Side

**File:** `Program.cs`

**What happens:** `options.IdleTimeout = TimeSpan.FromDays(365)` means a stolen session cookie is valid for a year. There is no server-side session store that can be invalidated on logout — `Session.Clear()` only clears the local copy; the cookie remains valid until it expires on the client.

**Correct fix:** Use a distributed session store (Redis) with a short TTL (15–30 minutes). On logout, delete the server-side session entry by key. Use HttpOnly + Secure + SameSite=Strict cookies.

---

## Bug 7: SQL Errors Swallowed Silently

**Files:** All `PageModel` classes

**What happens:** Every DB call is wrapped in `catch { }` with no logging. If the database is down, the user sees a blank page, an empty dashboard, or is silently redirected. No error ID is generated, no alert is raised, no stack trace is logged.

**Correct fix:** Use structured logging (Serilog / Microsoft.Extensions.Logging) with a correlation ID per request. Let exceptions propagate to a global error handler that logs full context and shows a user-friendly error page with a reference ID.

---

## Bug 8: Hardcoded Connection String with Plaintext Credentials

**Files:** `appsettings.json`, all `PageModel` classes (FALLBACK_CONN constant)

**What happens:** The database password `nordiska123` appears in source code committed to git. Anyone with read access to the repository has the database credentials.

**Correct fix:** Use environment variables or a secrets manager (Azure Key Vault, AWS Secrets Manager, HashiCorp Vault). In development, use `dotnet user-secrets`. Never commit credentials.
