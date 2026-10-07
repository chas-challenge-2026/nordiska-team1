<div align="center">

# Nordiska Database

### Current PostgreSQL schema overview

[Overview](#overview) ·
[Banking](#banking-schema) ·
[FAQ](#faq-schema) ·
[Inbox](#inbox-schema) ·
[Reporting](#reporting-schema) ·
[Module boundaries](#module-boundaries)

</div>

---

## Overview

| Schema | Responsibility | Tables |
|---|---|---:|
| `banking` | Customers, authentication, accounts and transactions | 14 |
| `faq` | FAQ content and search history | 2 |
| `inbox` | Messages, documents, terms and notifications | 10 |
| `reporting` | Report snapshots, generation jobs and PDF metadata | 9 |
| **Total** |  | **35** |

```mermaid
flowchart LR
    Banking["BANKING<br/>Customers, accounts<br/>and transactions"]
    FAQ["FAQ<br/>Questions and<br/>search history"]
    Inbox["INBOX<br/>Messages, documents<br/>and terms"]
    Reporting["REPORTING<br/>Snapshots, jobs<br/>and PDF metadata"]

    Banking -. "CustomerId" .-> Inbox
    Banking -. "CustomerId / AccountId" .-> Reporting
    Banking -. "SourceLedgerEntryId" .-> Reporting

    classDef banking fill:#2563eb,color:#ffffff,stroke:#1d4ed8
    classDef faq fill:#9333ea,color:#ffffff,stroke:#7e22ce
    classDef inbox fill:#059669,color:#ffffff,stroke:#047857
    classDef reporting fill:#ea580c,color:#ffffff,stroke:#c2410c

    class Banking banking
    class FAQ faq
    class Inbox inbox
    class Reporting reporting
```

### Relationship legend

| Diagram | Meaning |
|---|---|
| Solid relationship | Enforced database foreign key |
| Dotted relationship | Scalar reference across module boundaries |
| `PK` | Primary key |
| `FK` | Foreign key |

> Cross-schema identifiers such as `CustomerId`, `AccountId` and
> `SourceLedgerEntryId` are intentionally stored as scalar values.
> PostgreSQL does not enforce foreign keys between the modules.

---

## Banking schema

<details open>
<summary><strong>Show banking ER diagram</strong></summary>

<br/>

```mermaid
erDiagram
    BANKING_CUSTOMERS {
        bigint Id PK
    }

    BANKING_SAVINGS_ACCOUNTS {
        bigint Id PK
        bigint CustomerId FK
        string AccountType FK
    }

    BANKING_LEDGER_ENTRIES {
        bigint Id PK
        bigint AccountId FK
    }

    BANKING_ACCOUNT_TYPE_CONFIGS {
        string AccountType PK
    }

    BANKING_ACCOUNT_TYPE_RATE_HISTORIES {
        bigint Id PK
        string AccountType FK
    }

    BANKING_LOANS {
        bigint Id PK
        bigint CustomerId FK
    }

    BANKING_NOTIFICATIONS {
        bigint Id PK
    }

    BANKING_OPERATIONAL_MESSAGES {
        bigint Id PK
    }

    BANKING_ASPNET_ROLES {
        bigint Id PK
    }

    BANKING_ASPNET_ROLE_CLAIMS {
        bigint Id PK
        bigint RoleId FK
    }

    BANKING_ASPNET_USER_CLAIMS {
        bigint Id PK
        bigint UserId FK
    }

    BANKING_ASPNET_USER_LOGINS {
        string LoginProvider PK
        string ProviderKey PK
        bigint UserId FK
    }

    BANKING_ASPNET_USER_ROLES {
        bigint UserId PK, FK
        bigint RoleId PK, FK
    }

    BANKING_ASPNET_USER_TOKENS {
        bigint UserId PK, FK
        string LoginProvider PK
        string Name PK
    }

    BANKING_CUSTOMERS ||--o{ BANKING_SAVINGS_ACCOUNTS : owns
    BANKING_CUSTOMERS ||--o{ BANKING_LOANS : has

    BANKING_SAVINGS_ACCOUNTS ||--o{ BANKING_LEDGER_ENTRIES : contains

    BANKING_ACCOUNT_TYPE_CONFIGS ||--o{ BANKING_SAVINGS_ACCOUNTS : configures
    BANKING_ACCOUNT_TYPE_CONFIGS ||--o{ BANKING_ACCOUNT_TYPE_RATE_HISTORIES : tracks

    BANKING_ASPNET_ROLES ||--o{ BANKING_ASPNET_ROLE_CLAIMS : contains

    BANKING_CUSTOMERS ||--o{ BANKING_ASPNET_USER_CLAIMS : has
    BANKING_CUSTOMERS ||--o{ BANKING_ASPNET_USER_LOGINS : has
    BANKING_CUSTOMERS ||--o{ BANKING_ASPNET_USER_ROLES : assigned
    BANKING_CUSTOMERS ||--o{ BANKING_ASPNET_USER_TOKENS : owns

    BANKING_ASPNET_ROLES ||--o{ BANKING_ASPNET_USER_ROLES : contains
```

</details>

[Back to overview](#overview)

---

## FAQ schema

<details>
<summary><strong>Show FAQ ER diagram</strong></summary>

<br/>

```mermaid
erDiagram
    FAQ_ENTRIES {
        bigint Id PK
    }

    FAQ_SEARCH_LOG {
        bigint Id PK
    }
```

> The FAQ tables currently have no enforced foreign-key relationship.

</details>

[Back to overview](#overview)

---

## Inbox schema

<details>
<summary><strong>Show Inbox ER diagram</strong></summary>

<br/>

```mermaid
erDiagram
    INBOX_DOCUMENTS {
        bigint Id PK
    }

    INBOX_CUSTOMER_DOCUMENTS {
        bigint Id PK
        bigint DocumentId FK
        bigint CustomerId
    }

    INBOX_TERMS {
        bigint Id PK
        bigint DocumentId FK
    }

    INBOX_TERM_ACCEPTANCES {
        bigint Id PK
        bigint TermId FK
        bigint CustomerId
    }

    INBOX_MESSAGE_BOXES {
        bigint Id PK
        bigint CustomerId
    }

    INBOX_MESSAGE_THREADS {
        bigint Id PK
        bigint MessageBoxId FK
    }

    INBOX_MESSAGES {
        bigint Id PK
        bigint ThreadId FK
        bigint SenderCustomerId
    }

    INBOX_MESSAGE_THREAD_STATES {
        bigint Id PK
        bigint ThreadId FK
        bigint CustomerId
    }

    INBOX_CUSTOMER_NOTIFICATIONS {
        bigint Id PK
        bigint CustomerId
    }

    INBOX_FEED_ITEMS {
        bigint Id PK
        bigint CustomerId
    }

    INBOX_DOCUMENTS ||--o{ INBOX_CUSTOMER_DOCUMENTS : assigned
    INBOX_DOCUMENTS ||--o{ INBOX_TERMS : represents

    INBOX_TERMS ||--o{ INBOX_TERM_ACCEPTANCES : accepted

    INBOX_MESSAGE_BOXES ||--o{ INBOX_MESSAGE_THREADS : contains
    INBOX_MESSAGE_THREADS ||--o{ INBOX_MESSAGES : contains
    INBOX_MESSAGE_THREADS ||--o{ INBOX_MESSAGE_THREAD_STATES : tracks
```

</details>

[Back to overview](#overview)

---

## Reporting schema

<details open>
<summary><strong>Show Reporting ER diagram</strong></summary>

<br/>

```mermaid
erDiagram
    REPORTING_TAX_REPORTS {
        bigint Id PK
        bigint CustomerId
        bigint AccountId
    }

    REPORTING_TAX_REPORT_JOBS {
        bigint Id PK
        bigint TaxReportId FK
    }

    REPORTING_ACCOUNT_STATEMENTS {
        bigint Id PK
        bigint CustomerId
        bigint AccountId
    }

    REPORTING_ACCOUNT_STATEMENT_ENTRIES {
        bigint Id PK
        bigint AccountStatementId FK
        bigint SourceLedgerEntryId
    }

    REPORTING_ACCOUNT_STATEMENT_JOBS {
        bigint Id PK
        bigint AccountStatementId FK
    }

    REPORTING_GENERATED_DOCUMENTS {
        bigint Id PK
    }

    REPORTING_TAX_REPORT_DOCUMENTS {
        bigint TaxReportId PK, FK
        bigint DocumentId PK, FK
    }

    REPORTING_ACCOUNT_STATEMENT_DOCUMENTS {
        bigint AccountStatementId PK, FK
        bigint DocumentId PK, FK
    }

    REPORTING_AUDIT_ENTRIES {
        bigint Id PK
        bigint UserId
    }

    REPORTING_TAX_REPORTS ||--o{ REPORTING_TAX_REPORT_JOBS : schedules
    REPORTING_TAX_REPORTS ||--o{ REPORTING_TAX_REPORT_DOCUMENTS : links

    REPORTING_GENERATED_DOCUMENTS ||--o{ REPORTING_TAX_REPORT_DOCUMENTS : referenced_by

    REPORTING_ACCOUNT_STATEMENTS ||--o{ REPORTING_ACCOUNT_STATEMENT_ENTRIES : contains
    REPORTING_ACCOUNT_STATEMENTS ||--o{ REPORTING_ACCOUNT_STATEMENT_JOBS : schedules
    REPORTING_ACCOUNT_STATEMENTS ||--o{ REPORTING_ACCOUNT_STATEMENT_DOCUMENTS : links

    REPORTING_GENERATED_DOCUMENTS ||--o{ REPORTING_ACCOUNT_STATEMENT_DOCUMENTS : referenced_by
```

</details>

[Back to overview](#overview)

---

## Module boundaries

The modules share identifiers without creating cross-schema database
foreign keys.

```mermaid
flowchart LR
    Customer["banking.customers"]
    Account["banking.savings_accounts"]
    Ledger["banking.ledger_entries"]

    InboxCustomer["Inbox customer records"]
    TaxReport["reporting.tax_reports"]
    Statement["reporting.account_statements"]
    StatementEntry["reporting.account_statement_entries"]
    Audit["reporting.audit_entries"]

    Customer -. "CustomerId" .-> InboxCustomer
    Customer -. "CustomerId" .-> TaxReport
    Customer -. "CustomerId" .-> Statement
    Customer -. "UserId" .-> Audit

    Account -. "AccountId" .-> TaxReport
    Account -. "AccountId" .-> Statement

    Ledger -. "SourceLedgerEntryId" .-> StatementEntry

    classDef banking fill:#2563eb,color:#ffffff,stroke:#1d4ed8
    classDef inbox fill:#059669,color:#ffffff,stroke:#047857
    classDef reporting fill:#ea580c,color:#ffffff,stroke:#c2410c

    class Customer,Account,Ledger banking
    class InboxCustomer inbox
    class TaxReport,Statement,StatementEntry,Audit reporting
```

### Why no cross-schema foreign keys?

- Each module owns its own schema.
- Reporting stores immutable historical snapshots.
- The Reporting Worker only receives access to the reporting data it needs.
- Modules can evolve without creating tightly coupled database migrations.
- Least-privilege database permissions remain easier to enforce.

---

<div align="center">

Current schemas: `banking` · `faq` · `inbox` · `reporting`

</div>
