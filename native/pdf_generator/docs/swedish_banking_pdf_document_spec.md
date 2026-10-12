# Swedish Banking PDF Documents — Implementation Specification

## 1. Purpose

This document specifies the customer-facing PDF documents required or useful for a Swedish banking application, with emphasis on deposit accounts, savings accounts, interest, tax reporting, and account transactions.

The specification is intended to guide:

- domain and document-model design,
- PDF template design,
- annual batch generation,
- on-demand document generation,
- consistency between customer documents and statutory reporting,
- validation and test planning.

It intentionally does **not** specify implementation code, PDF libraries, serialization, database design, or rendering technology.

The PDF layer should present already-calculated and already-validated banking data. It should not calculate interest, tax, balances, ownership allocation, or foreign-exchange values.

---

# 2. Document Set

The application should support the following document types.

| Internal document concept | Recommended Swedish customer title | Purpose |
|---|---|---|
| Annual statement | **Årsbesked** | Annual summary of accounts, balances, interest and relevant tax information |
| Interest statement | **Räntebesked** or **Specifikation av ränta** | Explain how interest was calculated and credited |
| Tax statement | **Kontrolluppgiftsbesked – Ränteinkomst** or **Skatteuppgift** | Show the customer which tax information was reported to Skatteverket |
| Account statement | **Kontoutdrag** | Transaction history and account balance movement |
| Statement of fees | **Redovisning av avgifter** | Statutory annual fee/rate statement for applicable consumer payment accounts |

These documents do not have equal legal status.

- **Årsbesked** is a normal Swedish banking document but does not have one universal prescribed PDF layout.
- **Räntebesked** is primarily an explanatory customer document and can be designed by the bank.
- **Kontrolluppgiftsbesked** represents statutory tax data, but the customer PDF itself is not the electronic KU20 submission.
- **Kontoutdrag** is a bank-designed document, but payment-services law regulates information that must be made available about relevant payment transactions.
- **Redovisning av avgifter** is different: its format is standardized under EU rules and should be implemented from the prescribed template rather than designed freely.

---

# 3. General Architecture Rule

The PDF subsystem must be a **presentation layer**, not a banking-calculation layer.

The recommended conceptual flow is:

**Banking/accounting domain → validated document data → PDF renderer → immutable PDF artifact**

The banking domain is responsible for deciding:

- balances,
- ownership shares,
- transaction amounts,
- interest accrual,
- credited interest,
- gross interest,
- tax withheld,
- reportable tax amounts,
- currency conversions,
- exchange rates,
- fee amounts,
- reporting periods,
- whether a value belongs to a given tax year.

The PDF subsystem is responsible for:

- document selection,
- layout,
- headings,
- tables,
- pagination,
- formatting numbers,
- formatting dates,
- presenting supplied values,
- presenting bank/customer/account identifiers,
- document metadata,
- producing deterministic output where required.

The renderer must **not independently reproduce business calculations** already performed elsewhere.

This is especially important for tax documents. The customer-facing tax PDF and the electronic KU20 submission should both derive from the same canonical tax data.

---

# 4. Shared Document Requirements

All bank-generated PDFs should follow a common document identity model.

## 4.1 Bank identity

Every document should identify the issuing institution sufficiently clearly.

Recommended fields:

- legal company name,
- organization number,
- registered/contact address,
- customer-service contact details,
- website,
- bank logo,
- applicable registration or institution information where appropriate.

Do not rely on the logo alone for issuer identification.

---

## 4.2 Customer identity

Documents should identify the intended customer.

Possible fields:

- full name,
- customer number,
- personal identity number or coordination number where appropriate,
- address where relevant.

Sensitive identifiers should be displayed according to the bank's security and privacy policy. A full personal identity number does not have to be shown merely because the system stores one.

---

## 4.3 Account identity

Where a document refers to a specific account, provide enough information for the customer to distinguish it from other accounts.

Possible fields:

- account name/product name,
- account number,
- masked account number where appropriate,
- IBAN where relevant,
- account currency,
- account ownership.

For multi-account documents, give each account a stable visible identifier.

---

## 4.4 Period

Every period-based document should clearly state:

- start date,
- end date,
- applicable income year where relevant,
- generation date where useful.

Do not make the customer infer the period from individual transactions.

---

## 4.5 Monetary formatting

For Swedish documents:

- use Swedish-readable number formatting,
- show currency explicitly,
- keep decimal precision appropriate to the underlying product,
- distinguish positive and negative transactions clearly,
- avoid silently rounding values that must reconcile with accounting totals.

Tax-reporting values may have different rounding requirements from customer ledger values. Those must be decided by the tax/business layer and supplied to the renderer separately.

---

## 4.6 Dates

Use an unambiguous Swedish date representation consistently.

Examples of suitable styles:

- 2026-12-31
- 31 december 2026

Do not mix date conventions inside the same document without reason.

For transaction documents, distinguish where necessary between:

- booking date,
- transaction/order date,
- value date.

---

## 4.7 Page identity

Multi-page PDFs should include:

- document title or short document identifier,
- page number,
- total pages where technically practical,
- account/customer reference where useful,
- document ID or generation ID where useful.

Example concept:

**Sida 2 av 4**

---

## 4.8 Document identifiers

Every generated artifact should have a stable internal document identifier.

This is useful for:

- audit trails,
- customer support,
- regeneration,
- version comparison,
- duplicate detection,
- tax-report traceability,
- proving which document was made available to a customer.

The visible PDF may include the document ID in the footer or metadata.

---

## 4.9 Versioning

Separate at least:

- document type,
- template version,
- business-data version,
- generation timestamp.

A regulatory change to KU fields or the EU fee template must not silently alter the historical interpretation of previously generated documents.

---

# 5. Årsbesked

## 5.1 Purpose

The **Årsbesked** is the customer's high-level annual overview.

It should answer:

- Which accounts did I have?
- What was the balance at year-end?
- How much interest did I receive?
- How much tax was withheld?
- What relevant amounts were reported for tax purposes?

It should not normally contain every transaction from the year.

---

## 5.2 Generation

Typical generation:

- once per calendar year,
- after annual balances and interest have been finalized,
- normally made available early in the following year.

The generation process should use a closed and validated annual dataset rather than live account values queried during rendering.

---

## 5.3 Recommended document structure

### Header

Include:

- bank identity,
- title: **Årsbesked**,
- income/calendar year,
- customer identity,
- customer number,
- generation date.

### Account summary

For each account:

- account/product name,
- account identifier,
- currency,
- ownership information where relevant,
- year-end balance,
- optionally opening balance,
- optionally account status if closed during the year.

### Interest summary

For each relevant account:

- gross interest,
- tax withheld,
- net credited interest where relevant.

### Tax information

Where applicable, show the amount represented in statutory tax reporting.

Use wording such as:

**Uppgifter rapporterade till Skatteverket**

Possible values:

- reportable interest income,
- tax withheld,
- income year.

Do not imply that every value printed in the årsbesked is itself a KU field.

### Totals

Where multiple accounts are present, provide customer-level totals where they are meaningful.

Totals must be mathematically reconcilable with the underlying rows, allowing for documented tax/reporting rounding rules.

### Footer

Include:

- bank legal identity,
- contact details,
- document ID,
- page numbering.

---

## 5.4 Joint accounts

For jointly owned accounts, distinguish between:

- total account amount,
- customer's ownership/reporting share,
- amount actually reported for that customer.

Do not present the total account interest as if it were necessarily the customer's personal taxable amount.

Where ownership shares are known, the tax/business layer should provide the correct allocated share.

Where the relevant reporting rules require equal allocation because shares are not known, that decision must occur before PDF generation.

---

## 5.5 Foreign-currency accounts

The årsbesked may present original-currency values while tax reporting may require SEK amounts.

Keep these concepts visibly distinct.

Recommended presentation:

- account balance in account currency,
- interest in account currency,
- tax-reporting amount in SEK,
- optional explanatory note about conversion.

The PDF renderer must not independently choose the exchange rate.

---

# 6. Räntebesked / Specifikation av ränta

## 6.1 Purpose

This document explains the origin of an interest amount.

It should answer:

- What balance or balance periods were used?
- What rate applied?
- When did rates change?
- How much interest accrued?
- When was interest credited?
- What tax was withheld?
- How did the bank arrive at the final amount?

This document is particularly useful for customer support, transparency, account reconciliation, and verification.

---

## 6.2 Recommended title

Prefer one of:

- **Räntebesked**
- **Specifikation av ränta**
- **Ränteberäkning**

The term **Ränteunderlag** can be used internally, but it is less clear as a customer-facing document name because it can sound like an input basis rather than the final explanation.

---

## 6.3 Recommended contents

### Identification

Include:

- bank,
- customer,
- account,
- currency,
- period.

### Interest method

Describe the relevant product method in human-readable terms.

Possible information:

- whether interest is calculated daily,
- applicable balance basis,
- applicable day-count convention,
- when interest is credited,
- whether rates changed during the period.

Do not invent or generalize a method across account products. The actual product rules should supply this information.

### Interest periods

For each relevant rate period, show:

- period start,
- period end,
- applicable interest rate,
- applicable interest basis where useful,
- interest attributable to the period.

If there are hundreds of daily accrual entries, do not automatically print each one. Aggregate into understandable periods unless the customer explicitly needs a transaction-level or day-level calculation statement.

### Summary

Show separately:

- accrued interest,
- credited/paid interest,
- gross interest,
- tax withheld,
- net amount credited.

Only show concepts that actually apply to the product.

---

## 6.4 Critical domain distinction

The data model must distinguish:

- **accrued interest**,
- **credited interest**,
- **paid interest**,
- **gross interest**,
- **tax withheld**,
- **tax-reportable interest**.

These values may coincide in simple products but they are not conceptually identical.

The PDF layer must not collapse them into one field merely because a current product happens to produce equal values.

---

# 7. Tax Statement / Kontrolluppgiftsbesked

## 7.1 Purpose

This customer document explains which relevant interest/tax information the institution reported to Skatteverket.

Recommended title:

**Kontrolluppgiftsbesked – Ränteinkomst**

Alternative customer-friendly title:

**Skatteuppgift**

with a subtitle such as:

**Uppgifter rapporterade till Skatteverket**

---

## 7.2 Important architecture distinction

The PDF is **not** the KU20 submission.

For income year 2026, KU20 and KU25 are submitted digitally to Skatteverket.

Therefore the system should conceptually create one validated tax-reporting dataset and use it for two different outputs:

1. statutory digital reporting to Skatteverket,
2. customer-readable tax information.

Do not generate statutory reporting by parsing previously generated PDFs.

Do not independently calculate the customer PDF and KU20 payload.

---

## 7.3 KU20 fields relevant to deposit interest

Current Skatteverket KU20 documentation includes, among other fields:

- **203 — Inkomstår**
- **570 — Specifikationsnummer**
- **201 — Organisationsnummer for the reporting entity**
- **001 — Avdragen skatt**
- **500 — Ränteinkomst, konto**

The tax subsystem should follow the current Skatteverket technical specification rather than treating this document as a permanent list of all reporting fields.

The PDF should expose relevant customer-understandable information and may optionally display field numbers for traceability.

---

## 7.4 Recommended customer-PDF contents

Include:

### Reporting entity

- legal name,
- organization number.

### Recipient

- customer name,
- suitable recipient identifier.

### Reporting context

- income year,
- specification number where useful.

### Reported amounts

At minimum where applicable:

- interest income,
- tax withheld.

Clearly state that these are the amounts reported or intended for statutory reporting.

Do not mix unreconciled account-calculation values into this section.

---

## 7.5 Gross versus net interest

Tax reporting should clearly distinguish:

- gross interest income,
- tax withheld,
- resulting customer credit where relevant.

Do not report only the net account credit if the statutory value is gross interest.

---

## 7.6 Joint accounts

The customer tax statement should show the recipient's reportable share, not merely the account-level total.

If useful for clarity, show both:

- account total,
- customer's allocated share.

The legal/reporting allocation should be produced by the tax domain before rendering.

---

## 7.7 Foreign currency

When interest arises in foreign currency, preserve in the domain model:

- original amount,
- original currency,
- conversion date/basis,
- exchange rate used,
- reportable SEK amount.

The customer PDF may show both the original-currency amount and the SEK tax-reporting amount.

The rendering layer must not query exchange rates or perform the conversion itself.

---

## 7.8 Corrections

The system must support the concept of corrected tax statements.

A corrected customer statement should be distinguishable from the original.

Recommended metadata:

- original document reference,
- correction date,
- new document ID,
- tax specification number,
- indication that the document replaces or corrects an earlier version.

Do not overwrite an already distributed annual tax artifact without retaining an audit trail.

---

# 8. Kontoutdrag

## 8.1 Purpose

The **Kontoutdrag** provides transaction history and balance movement for a specified account and period.

It should enable the customer to:

- identify transactions,
- understand debits and credits,
- see opening and closing balances,
- reconcile the account,
- understand fees and currency conversion where applicable.

---

## 8.2 Generation modes

Support at least:

- user-selected date range,
- monthly statement,
- annual statement if requested,
- closed-account final statement where relevant.

Large statements should paginate correctly and remain readable when a period contains many transactions.

---

## 8.3 Header

Include:

- title: **Kontoutdrag**,
- bank identity,
- customer identity,
- account name,
- account identifier,
- account currency,
- statement period.

---

## 8.4 Opening and closing balances

Where the account model supports it, show:

- opening balance,
- closing balance.

The closing balance should reconcile with the opening balance and included booked transactions under the account's accounting rules.

Do not silently derive balances from displayed rows if the authoritative ledger supplies them.

---

## 8.5 Transaction information

The underlying document data should be capable of representing:

- transaction identifier,
- booking date,
- value date,
- transaction/order date where relevant,
- transaction type,
- counterparty,
- payment recipient or payer,
- reference/message,
- original amount,
- original currency,
- account amount,
- account currency,
- fee,
- exchange rate,
- balance after transaction.

Not every field must be printed for every transaction.

The template should select relevant information based on transaction type.

---

## 8.6 Minimum useful visible columns

For a normal account statement, a practical default is:

- date,
- description / counterparty,
- reference where useful,
- amount,
- balance.

Additional transaction details can be printed on a second line or supplementary section where needed.

---

## 8.7 Payment-services information requirements

Swedish payment-services law requires relevant information to be made available concerning payment transactions.

For transactions under a framework agreement, this includes information enabling identification of the transaction and, where appropriate, the recipient, as well as the transaction amount, fees and applicable value-date/date information.

Where currency conversion applies, exchange-rate information is also relevant.

The PDF template should therefore be capable of displaying all legally required transaction data even if the normal compact view does not always require every possible field.

---

## 8.8 Monthly information

For applicable payment-service arrangements, the law permits transaction information to be made available periodically, including arrangements in which it is made available monthly.

The system should therefore treat **monthly statement generation** as a first-class supported use case rather than only allowing arbitrary ad-hoc exports.

---

## 8.9 Long transaction descriptions

The renderer must handle:

- long recipient names,
- OCR references,
- bank-giro/plus-giro references,
- international payment references,
- multiline descriptions,
- Unicode characters.

Do not truncate important transaction identifiers merely to preserve a fixed one-line row.

---

## 8.10 Pagination

Do not split a single transaction row in a confusing way across pages.

Recommended behavior:

- repeat table headings on each page,
- carry forward account/document identity,
- keep transaction details together where practical,
- make page transitions deterministic.

---

# 9. Redovisning av avgifter

## 9.1 Purpose

For applicable consumer payment accounts, the customer must receive periodic information about fees and interest rates.

This document should be treated separately from the årsbesked.

---

## 9.2 Do not design a custom template

The EU has prescribed a standardized **Statement of Fees** presentation.

The Swedish title is:

**Redovisning av avgifter**

For this document, do not apply the application's ordinary bank-PDF layout freely.

Implement the prescribed template.

---

## 9.3 Core formatting requirements from EU Regulation 2018/33

The regulation requires, among other things:

- A4 portrait format,
- title **Redovisning av avgifter** at the top of the first page,
- bank logo in the upper-left area,
- common EU symbol in the upper-right area,
- prescribed ordering of information,
- prescribed headings and subheadings,
- Arial or similar typeface,
- specified general and heading font sizes,
- largely grayscale presentation,
- numbered pages.

The regulation states that providers must use the template and must not alter its information order, headings or subheadings outside the permitted rules.

Therefore this document should have a dedicated compliance-controlled renderer/template.

---

## 9.4 Data expected by the template

The domain/document data should be able to supply:

- account identification,
- statement period,
- service names using applicable standardized terminology,
- number of times a service was used where applicable,
- unit fee,
- total fee for the service,
- total fees,
- relevant interest paid,
- relevant interest earned,
- applicable interest rates where required.

Exact current fields and terminology must follow the applicable EU and Swedish rules at implementation time.

---

# 10. Recommended Relationship Between Annual Documents

Do not assume that every customer needs five separate PDFs every year.

A practical product policy may be:

### Årsbesked

Always generate for relevant savings/deposit customers.

Contains:

- account summary,
- year-end balances,
- annual interest,
- tax overview.

### Räntebesked

Generate:

- on demand,
- when detailed calculation disclosure is needed,
- optionally automatically for products with complex interest rules.

### Kontrolluppgiftsbesked

Generate:

- when the institution wants a distinct customer artifact showing reported tax information,
- or incorporate the corresponding tax-information section into the Årsbesked if compliance and product policy permit.

### Kontoutdrag

Generate:

- on demand,
- monthly where applicable,
- according to account/customer agreements.

### Redovisning av avgifter

Generate separately where legally applicable because it has its own standardized presentation.

---

# 11. Annual Processing Model

A recommended annual business sequence is:

1. Close or freeze the relevant calendar-year accounting period.
2. Validate account balances.
3. Finalize all interest accrual/crediting that belongs to the year.
4. Determine gross interest and tax withholding.
5. Allocate amounts between joint owners.
6. Convert reportable foreign-currency amounts according to the applicable tax rules.
7. Build customer-level tax-reporting data.
8. Validate statutory reporting totals.
9. Generate statutory KU20 output.
10. Generate customer tax information from the same canonical dataset.
11. Generate Årsbesked from the validated annual account dataset.
12. Generate applicable Redovisning av avgifter independently according to its prescribed rules.
13. Store document IDs, template versions and generation metadata.
14. Make customer artifacts available through the banking application.

The exact order may differ operationally, but PDFs should not be generated from incomplete year-end calculations.

---

# 12. Canonical Document Data

The rendering system should receive document-specific canonical data rather than raw database rows.

Conceptually separate at least the following data sets.

## 12.1 Customer data

- customer identity,
- customer number,
- display-safe identity information,
- address where relevant.

## 12.2 Account data

- account identity,
- product,
- currency,
- ownership,
- account status.

## 12.3 Balance data

- opening balance where relevant,
- closing/year-end balance,
- currency.

## 12.4 Interest data

- interest periods,
- rates,
- accrual values,
- credited values,
- gross interest,
- tax withheld,
- net credit.

## 12.5 Tax data

- income year,
- reporting entity,
- recipient,
- specification number,
- reportable interest,
- tax withheld,
- original-currency information where needed,
- SEK reportable amount,
- correction/replacement metadata.

## 12.6 Transaction data

- identification,
- dates,
- parties,
- references,
- amounts,
- currencies,
- fees,
- FX data,
- resulting balance.

## 12.7 Fee-statement data

- standardized service,
- usage,
- unit charge,
- total charge,
- interest information,
- summary totals.

---

# 13. Validation Requirements

The PDF generator should reject or flag structurally invalid document data rather than producing plausible-looking but incorrect documents.

Validation responsibilities should be divided between business validation and presentation validation.

---

## 13.1 Business validation before rendering

Examples:

- account exists,
- customer-account relationship is valid,
- reporting period is valid,
- year-end figures are finalized,
- ownership allocation is finalized,
- tax values are approved,
- currencies are known,
- reportable SEK values exist when required,
- transaction ordering is defined.

These are outside the PDF engine.

---

## 13.2 Presentation validation

The PDF/document layer should verify things such as:

- required title exists,
- required identity fields are present,
- required period is present,
- currency accompanies money values,
- no mandatory table is empty when the document type requires entries,
- page generation completed,
- template version is known,
- required statutory template assets are present,
- no unsupported document version is used.

---

# 14. Reconciliation Rules

Financial PDFs should reconcile against their source data.

Examples:

## Årsbesked

Account-level totals should reconcile to customer totals.

## Räntebesked

Detailed interest-period amounts should reconcile to the supplied annual/period summary subject to explicit rounding rules.

## Tax statement

Displayed tax amounts should match the canonical values used for statutory reporting.

## Kontoutdrag

Opening balance plus applicable account movements should reconcile with the authoritative closing balance according to ledger rules.

## Redovisning av avgifter

Detailed fee rows should reconcile with the required total fee amounts.

Do not hide discrepancies with renderer-side adjustment rows unless such adjustments are explicit business data.

---

# 15. Determinism and Reproducibility

For annual bank documents, deterministic output is desirable.

Given:

- identical canonical document data,
- identical template version,
- identical renderer version,
- identical fonts/assets,
- identical document metadata rules,

the renderer should aim to produce semantically identical output and, if the project requires it, byte-identical output.

Avoid injecting uncontrolled current timestamps, random identifiers or environment-dependent formatting into deterministic documents.

If the generation timestamp must appear in the PDF, make it an explicit input to the document generation request.

---

# 16. Immutability and Auditability

Once a customer document has been officially generated and distributed, treat it as an immutable artifact.

If data changes:

- generate a new document,
- record its relationship to the previous document,
- retain the original according to applicable retention policy,
- mark corrections clearly where relevant.

Store audit metadata separately from the PDF itself.

Recommended audit facts:

- document ID,
- customer ID,
- document type,
- source data version or snapshot ID,
- template version,
- renderer version,
- creation timestamp,
- reporting period,
- checksum/hash,
- replaced/corrected document ID where applicable.

---

# 17. Localization

Initial Swedish documents should use:

- Swedish titles,
- Swedish financial terminology,
- Swedish date/number formatting,
- SEK where required for statutory reporting.

Keep localization separate from underlying financial meaning.

Do not encode semantic differences merely as translated labels.

If English is added later, the English PDF should still represent the same canonical values and legal concepts.

The standardized **Redovisning av avgifter** terminology must follow the official applicable language/template rather than arbitrary translation.

---

# 18. Accessibility and PDF Quality

Where feasible, generated documents should support:

- selectable text,
- real text rather than rasterized pages,
- meaningful reading order,
- embedded or reliably available fonts,
- sufficient contrast,
- clear headings,
- table structures that remain understandable when copied or read with assistive technology.

Avoid treating a bank PDF as an image.

For the standardized fee statement, follow the accessibility allowances and format rules in the applicable regulation.

---

# 19. Security and Privacy

The PDF subsystem handles financial and identity information.

Requirements should include:

- no cross-customer data leakage,
- no reuse of buffers/data between customer documents without safe clearing/lifecycle management,
- strict authorization before document retrieval,
- predictable document storage paths or opaque identifiers that cannot be guessed across customers,
- secure temporary-file handling,
- safe logging.

Logs should not casually contain:

- full personal identity numbers,
- complete account numbers,
- transaction descriptions,
- balances,
- tax values.

Document IDs should support debugging without requiring sensitive financial content in logs.

---

# 20. Document Storage and Delivery

The generation system should distinguish:

- generated document,
- stored artifact,
- customer-visible document record.

A customer-visible record should normally include:

- document title,
- document type,
- period/year,
- generation/publication date,
- account where relevant,
- download reference,
- correction/replacement status.

Example categories in the UI:

- Årsbesked
- Skatteuppgifter
- Kontoutdrag
- Ränta
- Avgifter

---

# 21. Suggested MVP Scope

For an initial implementation focused on savings/deposit banking:

## Priority 1 — Årsbesked

Implement:

- customer identity,
- accounts,
- year-end balances,
- gross interest,
- withheld tax,
- tax-reporting summary,
- multi-account totals,
- pagination.

## Priority 2 — Kontoutdrag

Implement:

- arbitrary period,
- opening/closing balance,
- transaction list,
- references,
- transaction amounts,
- balances,
- pagination.

## Priority 3 — Kontrolluppgiftsbesked

Implement:

- income year,
- reporting entity,
- recipient,
- specification identifier,
- interest reported,
- tax withheld,
- correction support.

## Priority 4 — Räntebesked

Implement:

- rate periods,
- calculation basis information,
- gross/withheld/net summary.

## Priority 5 — Redovisning av avgifter

Implement when payment-account scope requires it.

Because this template is regulated, do not build it by adapting the ordinary annual-statement design.

---

# 22. Test Cases

At minimum, build fixtures for the following scenarios.

## Annual statement cases

- one customer, one SEK account,
- one customer, several accounts,
- zero interest,
- interest with tax withheld,
- closed account during the year,
- joint account,
- several currencies,
- very large monetary amounts,
- negative/edge balance if the product permits it.

## Interest statement cases

- constant rate all year,
- several rate changes,
- leap year,
- mid-year account opening,
- account closure,
- no credited interest,
- annual credit,
- monthly interest credit.

## Tax cases

- ordinary KU20 interest,
- tax withheld,
- joint ownership,
- foreign-currency interest,
- multiple valid specifications for one recipient,
- corrected submission/document,
- zero-value/non-reportable scenarios according to business rules.

## Account statement cases

- no transactions,
- one transaction,
- hundreds/thousands of transactions,
- multiline references,
- currency conversion,
- fees,
- international characters,
- transaction at page boundary,
- reversal/correction transaction.

## Fee statement cases

- no chargeable service use,
- repeated service,
- several fee categories,
- interest earned,
- interest charged,
- multi-page template.

---

# 23. Explicit Non-Goals of the PDF Layer

The PDF layer should **not** decide:

- tax law,
- tax eligibility,
- interest rates,
- day-count methods,
- which balance earns interest,
- customer ownership percentage,
- foreign-exchange rates,
- transaction settlement status,
- ledger balances,
- whether an account qualifies for a regulatory document,
- whether a statutory report must be filed.

Those decisions belong to upstream business/compliance systems.

The renderer may enforce document-structure requirements, but not invent financial meaning.

---

# 24. Regulatory Maintenance

Financial-document rules change.

Treat regulatory configuration as versioned product requirements.

At minimum, monitor:

- Skatteverket KU20/KU25 technical specifications,
- Swedish Payment Services Act requirements,
- Swedish implementation of payment-account rules,
- EU Statement of Fees template requirements,
- Finansinspektionen rules applicable to the institution and account products.

Do not hard-code today's KU field set into an assumption that it will never change.

Perform a compliance review before production deployment.

---

# 25. Primary Sources

The following sources should be treated as starting points for implementation and compliance review.

## Skatteverket — KU20 / KU25

**Kontrolluppgift om ränteutgift (KU25) och ränteinkomst (KU20)**  
https://www.skatteverket.se/foretag/skatterochavdrag/kontrolluppgifter/kontrolluppgiftomranteinkomstku20ochranteutgiftku25.4.1df9c71e181083ce6f6349.html

Important current point: from income year 2026, KU20 and KU25 are submitted digitally.

---

## Sveriges riksdag — Lag (2010:751) om betaltjänster

https://www.riksdagen.se/sv/dokument-och-lagar/dokument/svensk-forfattningssamling/lag-2010751-om-betaltjanster_sfs-2010-751/

Relevant areas include:

- information requirements for payment transactions,
- information supplied under framework agreements,
- monthly availability of transaction information,
- information concerning fees and exchange rates,
- annual information concerning fees/rates connected to payment accounts.

---

## EUR-Lex — Commission Implementing Regulation (EU) 2018/33

https://eur-lex.europa.eu/legal-content/SV/TXT/?uri=CELEX:32018R0033

This specifies the standardized presentation format and common symbol for **Redovisning av avgifter**.

Implement this document directly against the current official template and regulation.

---

# 26. Final Design Principle

Treat these PDFs as **official views of authoritative banking data**, not as places where banking logic happens.

A good implementation should make it possible to say:

> The banking/tax system decided the values.  
> The document model captured those values.  
> The PDF generator rendered them faithfully.  
> The generated artifact can be reproduced, audited and traced back to its source data.

That separation is the central design rule for the entire document subsystem.
