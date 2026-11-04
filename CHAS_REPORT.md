# Chas Challenge 2026: Project & Delivery Report

## Contents

- [Requirements and implementation](#requirements-and-implementation)
- [Testing and quality assurance](#testing-and-quality-assurance)
- [Specialist feed-forward](#specialist-feed-forward)
- [Architecture and technical decisions](#architecture-and-technical-decisions)
- [Known limitations and scope boundaries](#known-limitations-and-scope-boundaries)
- [Demo fallback and contingency plan](#demo-fallback-and-contingency-plan)
- [Methodology and process](#methodology-and-process)


## Requirements and implementation

| Customer pain point | User story | Technical implementation |
| :--- | :--- | :--- |
| **500 support tickets/week** for basic inquiries | Customers need clear access to information | Self-service dashboard with a clear and easy FAQ component  |
| **Postal delays** for statements and tax sheets | Customers require instantaneous documents | In-process C++23 PDF pipeline |
| **Balance race conditions** during transfers | Customers need guaranteed transactional integrity | Ledger-based double-entry accounting with `SELECT FOR UPDATE` pessimistic row locking |
| **Credential & session vulnerabilities** | Customers and bank require strict security compliance | BCrypt (work factor 12), HttpOnly secure cookies, 15-minute token TTL, SCRAM-SHA-256 DB auth |
| **FAQ returning irrelevant answers** | Customers need rapid self-help answers | PostgreSQL full-text search with Swedish dictionary stemming and `ts_rank` relevance scoring |
| **Admin visibility into customer issues** | Support team needs efficient inquiry management | Admin portal with thread management, search analytics, and customer satisfaction tracking |


## Testing and quality assurance

**Platform coverage:** 86.0% line, 67.3% branch.

| Module | Line | Branch | Core focus |
| :--- | :---: | :---: | :--- |
| **FAQ Module** | 99.0% | 94.6% | Swedish FTS, relevance ranking, SHA-256 analytics hashing |
| **Banking Module** | 92.6% | 71.7% | Ledger balance constraints, pessimistic transfer locking |
| **Inbox Module** | 89.8% | 64.4% | Customer thread state transitions, notification outbox |
| **Reporting Module** | 87.5% | 80.7% | Tax calculations, batch job staging, error handling |
| **Frontend API Layer** | 71.5% | 59.3% | Controllers, authentication cookies, middleware |

**Suites**
- **Backend (.NET):** xUnit, Moq, FluentAssertions, `WebApplicationFactory` for integrated HTTP pipelines. 60/60 worker tests and 54/54 reporting tests passing in Release.
- **Native PDF engine (C++):** CTest with AddressSanitizer and UndefinedBehaviorSanitizer. 17/17 suites passing in Debug (including adversarial fuzz tests), 13/13 in Release.
- **PDF Signer (C / PKCS#11):** Dedicated test suite (`make test-docker` with SoftHSM2) covering PKCS#11 key/cert loading, CMS signature generation, Unix domain socket concurrency and stress (`test_signer_stress`), and RFC 3161 TSA timestamping.
- **Frontend:** Vitest and React Testing Library for component interactions and state machines.

**Benchmark detail (10,000 customers)**
End-to-end batch processing for 10,000 customers (25,448 total documents: 10,004 tax reports and 15,444 account statements) completed in **52.5 seconds** on an 8-core laptop with zero failures (485 signed PDFs/sec across staging, worker polling, native rendering, CMS signing, and file persistence). Detailed module-level profiles and in-memory C ABI metrics are documented in the [benchmark documentation](tools/benchmark/README.md).

```text
Phase 1 - Job Staging (SQL):       1.24s   (2,039 jobs/sec)
Phase 2 - PDF Generation & Sign:   4.73s   (533 PDFs/sec)
Total Batch Pipeline Execution:    5.97s   (Complete dataset)

Latencies: P50: 7.6ms | P95: 13.8ms | P99: 18.1ms
Document Generation Success Rate:  100% (0 errors across 25,448 generated documents)
```

## Specialist feed-forward

Architectural and product decisions were shaped through structured review workshops with industry specialists.

### 1. CTO workshop: risk and security architecture
- **Specialist:** [Victor Axelsson](https://www.linkedin.com/in/victor-axelsson-469a18a3/), CTO at BraveOnion
- **Focus:** JWT storage trade-offs, token revocation, tamper-evident logging, database role isolation.
- **Implemented:**
  - Replaced browser `localStorage` tokens with `HttpOnly`, `SameSite=Lax`, `Secure` cookies.
  - 15-minute token lifetimes with sliding-window refresh.
  - 5-minute automatic inactivity logout.
  - Database role separation (separate credentials for migrations, API service, and reporting worker).
  - Tamper-evident HMAC-SHA256 audit log structures for security events.
  - Switched native pipeline to in process library because of customer data security.
  

### 2. Delivery management workshop: scope and execution
- **Specialist:** [Karl Lindau](https://www.linkedin.com/in/karl-lindau-95173612/), Delivery Manager & CEO at B3 Linköping
- **Focus:** Agile scope containment, critical path management, risk-mitigated demo planning.
- **Implemented:**
  - Prioritized core self-service flows (accounts, transactions, document downloads) over speculative edge features.


### 3. UX and accessibility workshop
- **Specialist:** [Emma Utsi](https://www.linkedin.com/in/emma-utsi/), UX Designer at CGI
- **Focus:** Accessibility across age groups, visual hierarchy, mobile usability, error prevention.
- **Implemented:**
  - Multi-step visual confirmations for balance-altering operations (transfers and withdrawals).
  - Responsive layouts across mobile, tablet, and desktop.
  - Increased the functionality of the bank account page
  - Improved the flow through the page with better hirachy of most used functions
  - Hid the contact info to the support team to improve flow of FAQ page

### 4. Client stakeholder feedback  
*(Frontend team: add client feedback items and UI adjustments implemented)*
- **Stakeholder:** [Kajsa Dymling](https://www.linkedin.com/in/kajsadymling/), Engineering Manager at Nordiska Sparbanken
- **Focus:** frontend should focus more on hard monetary gains and intrest percentages. Banking secrecy (PII protection), document layouts matching bank standards.
- **Implemented:**
  - We performed targeted user testing for the banks core customer target audience
  - Improved the visibility of interest, both for accrued and projected
  - Implemented intrest gains prognosis tooling for easy user comparision between account types 
  - Implemented the color palette of the current nordiska page and made them easy to change
  - Reporting diagnostics redesigned to keep customer PII out of error logs ([commit `8966e5c`](https://github.com/chas-challenge-2026/nordiska-team1/commit/8966e5c)).
  - PDF statement layouts aligned with banking reference standards ([Issue #283](https://github.com/chas-challenge-2026/nordiska-team1/issues/283)).


## Methodology and process

We found our selected system offered the team a great cadence between team sync and individual development. Linear issues ans sprint provided a good source of truth for project state and cross team issue tracking.   

- **Cadence:** Two synchronization meetings a week (monday, thursday), daily Slack coordination, iterative sprint deliverables.
- **Issue tracking:** Linear for backlog and progress visibility.
- **Leadership rotation:** Team lead rotated in alphabetical order, with the outgoing lead serving as secretary.
- **Governance:** Mandatory peer review on all pull requests with branch protection; pair programming across frontend, backend, and native boundaries.  
---

## Decision log
High level full project decisions:
- **In process PDF pipeline**: Or first implementation was a completely separate process. But during the risk analysis we realized that customer data is more important to protect than a single docker container crashing. So we switched to an in process RAM only PDF pipeline.
- **Signing**: The signing service is designed as a completely separate service that only receives a hash. Enabling easy swapping of signing services (internal or external) and safeguarding customer data.
- Modular monolith with separate PDF worker from API backend.
- **Admin portal**: Our analysis of the customers main pain points made us realize that an admin portal is essential for achieving the projects main goal. It allows customer service agents to keep a very tight feedback loop between the users on site behaviour and FAQ managment.
- **Database Roles**: all interactions with the database go through specific and well defined roles. Least priveledged access.

## Module specific decision logs

### Frontend 
- [Frontend](frontend/README.md#Frontend)

### Native
- [PDF Generator Architecture & Teststatus](native/pdf_generator/README.md)
- [Native PDF Generator ADR (Jimmy Jordan)](native/pdf_generator/ADR_jimmy_jordan.md)
- [PDF-signering Beslutslogg (Marcus Mandelström)](beslutslogg.md)

## Known limitations

- **Native platform:** PDF generator and signing service require Linux x86-64 / Docker runtime.
- **Identity provider:** Local Docker Compose environment uses real BankID but is currently set to test certificate.
- **Frontend / Backend limitations:** Customer portal and admin portal runs in the same container. Practical for demo, not good enough for production.
- GDPR explicitly out of scope for MVP.

## Demo fallback and contingency plan
Recording, script and slides are availible at teams google drive.
https://docs.google.com/presentation/d/1QXzZ_qw9iCk77pk-MTCtGoziUERpFGzdcLw3beF4hhE/edit?slide=id.h1c509b7052af1835_0_114#slide=id.h1c509b7052af1835_0_114
https://docs.google.com/document/d/1ZXMLQPMOq2r3jD_XDXZI-AbzhaSYWObPww1RSD2brHQ/edit?tab=t.plw4fhoc4r1v
https://drive.google.com/file/d/1BPRabFY82Es7WWAEAWRTdDUh9HT2sENz/view
