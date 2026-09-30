# Tisdags-Demo Changes Log

**Branch:** `temp/tisdags_demo`  
**Goal:** Connect the UI button to end-to-end PDF generation for the Tuesday demo.  
**Purpose of this document:** Keep a running, reproducible record of all configuration, build, and code adjustments so the team can review and adopt the changes smoothly.

---

## 1. Native Build & Docker Integration

### Problem
A fresh build of the Docker container fails during Stage 1 (`native-builder`) because:
1. `native/pdf_generator/CMakeLists.txt` depends on `../pdf-signer`, but `Dockerfile` only copied `native/pdf_generator/`.
2. Missing build tools and development libraries in Debian base image: `git`, `libssl-dev`, `zlib1g-dev`, and `libsimdjson-dev`.
3. Target name mismatch: `Dockerfile` was invoking `--target nordiska_document_c_api`, but the target in `CMakeLists.txt` is `nordiska_pdf_generator_c_api`.
4. Shared library naming: Stage 4 was trying to copy `libnordiska_document_c_api.so`, while CMake outputs `libnordiska_pdf_generator_c_api.so`.

### Changes Applied
1. **Updated Stage 1 (`native-builder`) in `Dockerfile`**:
   - Pinned exact Debian Bookworm package versions to eliminate silent version drift:
     - `cmake=3.25.1-1`
     - `pkg-config=1.8.1-1`
     - `libcairo2-dev=1.16.0-7`
     - `libhpdf-dev=2.3.0+dfsg-1+b1`
     - `nlohmann-json3-dev=3.11.2-2`
     - `libssl-dev=3.0.22-1~deb12u1`
     - `zlib1g-dev=1:1.2.13.dfsg-1`
     - `libsimdjson-dev=3.0.1-1`
   - Added `-DFETCHCONTENT_FULLY_DISCONNECTED=ON` to the CMake configuration to strictly forbid network access and prevent silent cloning from GitHub during builds.
   - Changed `COPY native/pdf_generator/ ./` to `COPY native/ native/` so that `native/pdf-signer` is available to the PDF generator build.
   - Updated CMake target invocation to `--target nordiska_pdf_generator_c_api` (matching the active target defined in `CMakeLists.txt`).
2. **Updated Stage 4 (`final`) in `Dockerfile`**:
   - Pinned exact runtime dependencies:
     - `libcairo2=1.16.0-7`
     - `libhpdf-2.3.0=2.3.0+dfsg-1+b1`
   - Updated copy source exclusively to the canonical library `libnordiska_pdf_generator_c_api.so` (discarding legacy naming entirely) followed by `ldconfig`.
3. **Verification**:
   - Executed full multi-stage Docker build with exact pins and offline enforcement: completed successfully with exit code 0 (`07d7ce881da1f1675a95adad7d19e4c330f8e68244f19`).

---

## 2. Backend C# P/Invoke Interface

### What I changed and why:
- **File:** `backend/src/Modules/Reporting/NativeCalls/PdfGenerationCalls.cs`
  - Switched `LibraryName` to `"nordiska_pdf_generator_c_api"` (the real `.so` library name).
  - Updated P/Invoke to call `nordiska_pdf_v1_generate_customer_batch` instead of the old `nordiska_document_generate_json`.
  - Added matching C# structs `NativePdfDocumentView` and `NativePdfBatchView` using `LayoutKind.Sequential` to read the batch delivered by native C++.
- **File:** `backend/src/Modules/Reporting/PdfGeneration/PdfGenerationService.cs`
  - Updated callback and state to unpack all documents in the batch into a dictionary keyed by `document_id`.
  - Added `GenerateBatch()` to return the entire document dictionary, and kept `Generate()` to return the requested PDF.
  - Removed obsolete `GetNativeVersion()`.
- **File:** `backend/src/Modules/Reporting/Infrastructure/PdfReportGenerator.cs`
  - Replaced dummy PDF stub with real call to `PdfGenerationService.Generate(json)`, mapping `TaxReportData` to the native `annual_tax_report` JSON schema.

---

## 3. Strongly Typed Batch DTOs & Localization

### What I changed and why:
- **File:** `backend/src/Modules/Reporting/PdfGeneration/PdfBatchDtos.cs`
  - Created strongly typed C# records (`CustomerBatchEnvelope`, `PdfDocumentEnvelope`, `AnnualTaxReportPayload`, `AccountStatementPayload`, `StatementTransactionPayload`).
  - Why: Decouples the .NET domain models from the JSON schema. Guarantees that .NET owns all calculations and localization formatting, sending pure pre-formatted strings to C++ so the native engine never needs to reformat or guess currencies.
- **File:** `backend/src/Modules/Reporting/Infrastructure/Db/DependencyInjection.cs`
  - Registered `PdfGenerationService` as a singleton in DI so `PdfReportGenerator` can inject it.
- **File:** `backend/src/Modules/Reporting/Infrastructure/PdfReportGenerator.cs`
  - Injected `PdfGenerationService` and formatted tax report numbers with Swedish culture (`sv-SE`), serializing the typed `CustomerBatchEnvelope` into JSON and invoking native generation.

---

## 4. Frontend UI Wiring

### What I changed and why:
- **File:** `frontend/src/services/reportsService.ts`
  - Added `downloadTaxReport(accountId, accountNumber, year)` helper calling `GET /api/reports/tax-report` with `responseType: "blob"`.
  - Downloads the PDF with an automatic file name `skatteunderlag_{year}_{accountNumber}.pdf`.
- **File:** `frontend/src/components/AccountCard.tsx`
  - Replaced empty `onClick={() => {}}` on the "Skatteunderlag" button with `handleDownloadTaxReport()`.
  - Added loading indicator (`"Laddar ner..."`) and inline error feedback if the report generation fails.

---

## 5. Account Statement & Multi-Document Archive (.NET Backend)

### What I changed and why:
- **File:** `backend/src/Modules/Reporting/Infrastructure/IPdfReportGenerator.cs` & `PdfReportGenerator.cs`
  - Added `GenerateStatementPdfAsync()` to format account transaction ledgers into `AccountStatementPayload` and render `kontoutdrag_{accountNumber}.pdf`.
  - Added `GenerateCustomerArchiveAsync()` to request a multi-document batch containing all tax reports and account statements, and bundle the resulting PDFs into a `.zip` file using `System.IO.Compression.ZipArchive`.
- **File:** `backend/src/Modules/Reporting/Application/ITaxReportService.cs` & `TaxReportService.cs`
  - Added `GenerateDirectStatementAsync()` and `GenerateCustomerArchiveAsync()` validating customer ownership before assembling domain ledger data.
- **File:** `backend/src/Nordiska.FrontendApi/Endpoints/Reporting/ReportsController.cs`
  - Added `GET /api/reports/statement?accountId={id}` returning single statement PDF.
  - Added `GET /api/reports/download-all` returning all documents bundled in a `.zip` archive.

---

## 6. Dedicated Documents Page & Navigation (`/documents`)

### What I changed and why:
- **File:** `frontend/src/services/reportsService.ts`
  - Added `downloadAccountStatement(accountId, accountNumber)` and `downloadAllDocuments()` to download statement PDFs and customer ZIP archives.
- **File:** `frontend/src/pages/DocumentsPage.tsx`
  - Created a dedicated, self-contained Documents page with zero modals.
  - Section 1: Dropdown for account selection + Dropdown for report type (Skatteunderlag vs Kontoutdrag) + "Ladda ner rapport" button.
  - Section 2: "Ladda ner alla dokument (ZIP)" button for the full customer portfolio archive.
- **File:** `frontend/src/routes/AppRoutes.tsx`
  - Added `/documents` route under `DesktopLayout`.
- **File:** `frontend/src/components/PageNavigation.tsx`
  - Added "Dokument" tab link in the desktop navigation bar.

---

## 7. Docker Runtime Dynamic Dependencies (`libsimdjson14` & GCC 13 `libstdc++`)

### What I changed and why:
- **File:** `Dockerfile` (Stage 4)
  - Installed runtime package `libsimdjson14=3.0.1-1` in Debian Bookworm.
  - Copied GCC 13's `libstdc++.so.6*` from `native-builder` into `/usr/local/lib/` and executed `ldconfig`.
  - **Why:** The native C++ library `libnordiska_pdf_generator_c_api.so` was built in Stage 1 with GCC 13 using `<format>` and `std::expected` (requiring `GLIBCXX_3.4.31` and `GLIBCXX_3.4.32`) and dynamically linked against `libsimdjson.so.14`. Without these, ASP.NET Core threw a runtime `DllNotFoundException` when attempting to P/Invoke the native library.

---

## 8. PDF Signature Verification & Findings

### Verification:
- Inspected generated PDFs (`skatteunderlag` and `kontoutdrag`).
- Adobe/ISO signature structure is correctly prepared:
  - AcroForm with `/SigFlags 3`
  - Signature dictionary: `/Type /Sig`, `/Filter /Adobe.PPKLite`, `/SubFilter /ETSI.CAdES.detached`
  - Calculated byte range: `/ByteRange [ 0 3568 11762 189 ]`
  - Signature contents slot: 8,192 hex characters (`/Contents <...>`).
- **Signature Status:** The contents slot currently consists of 8,192 zeroes (`0000...0000`).
- **Why:**
  1. `native/pdf_generator/src/c_api/pdf_generator_c_api.cpp` has `.enable_signing = false` intentionally set for C API callers.
  2. `native/pdf-signer/src/pdf_sign.c` requires a SoftHSM token (`/usr/lib/libsofthsm2.so`, PIN env var, local cert) not present in Docker, and the C signing function is currently an initial test stub returning repeating hex patterns.

---

## 9. Fix Statement Running Balances (`Saldo`) & Opening Balance Calculation

### What I changed and why:
- **File:** `backend/src/Modules/Reporting/Infrastructure/TaxReportService.cs`
  - Fixed `openingBalance` formula from `transactions.Sum(t => t.Amount)` to `closingBalance - transactions.Sum(t => t.Amount)`.
  - **Why:** The opening balance before a statement period must equal the closing balance minus the net sum of transactions during the period. Previously it set opening balance to the sum of transactions, causing `Ingående saldo` and `Utgående saldo` to both show `68 099,66 SEK`. Now `Ingående saldo` correctly shows `0,00 SEK`.
- **File:** `backend/src/Modules/Reporting/Infrastructure/PdfReportGenerator.cs`
  - Calculated chronological running balances starting from `OpeningBalance` and populated `BalanceAfterDisplay` for each transaction row.
  - **Why:** The `Saldo` column in the statement PDF was previously blank because `BalanceAfterDisplay: ""` was empty. It now displays the running account balance after every transaction.

---

## 10. Mobile Navigation & Demo Launch Instructions

### What I changed and why:
- **File:** `frontend/public/icons/documents.svg` & `frontend/src/components/PageNavigation.tsx`
  - Added "Dokument" button to the mobile bottom navigation bar so the documents page can be tested and accessed on phones and narrow viewports.
- **How to run the demo:**
  From the `infra` folder:
  ```bash
  cd infra && docker compose up -d --build app
  ```

---

## 11. Merged `origin/develop` & Conflict Resolution

### What I changed and why:
- **Merge conflict in `backend/src/Modules/Reporting/Infrastructure/PdfReportGenerator.cs`:**
  - `origin/develop` had an outdated stub calling `nordiska_document_generate_json`.
  - Kept our working implementation using `PdfGenerationService`, `CustomerBatchEnvelope`, and localized Swedish formatting for both `annual_tax_report` and `account_statement`, plus ZIP batch generation.
- **Merge conflict in `backend/src/Modules/Reporting/Infrastructure/TaxReportService.cs`:**
  - Integrated develop's new background report job architecture (`ITaxReportJobRepository`, `IReportFileStorage`, and `IReportDataBuilder`).
  - Preserved our direct document generation methods (`GenerateDirectStatementAsync`, `GenerateCustomerArchiveAsync`) and accurate balance calculation logic (`openingBalance = closingBalance - sum(transactions)`).
- **Navigation in `frontend/src/components/PageNavigation.tsx`:**
  - Retained both desktop and mobile "Dokument" navigation links alongside develop's navigation updates.

---

## 12. End-to-End Cryptographic PDF Signing Enabled

### What was changed and why:
1. **Integrated `origin/native-pdf-signer` with OpenSSL 3.3.2 in Docker:**
   - The signer module uses `CMS_final_digest(...)`, which was introduced in OpenSSL 3.2.0.
   - Added a multi-stage `openssl-builder` layer in `Dockerfile` compiling OpenSSL 3.3.2 once (fully cached by Docker) so the C library and .NET runtime have access to the modern CMS precomputed digest signing API.
   - Added a build fallback in `native/pdf-signer/src/pdf_sign.c` for OpenSSL < 3.2 to ensure host development tools (`ctest`, benchmarks) still compile cleanly everywhere.
2. **Hybrid Key & Certificate Loading (`pdf_signer_create`):**
   - Extended `pdf_signer_create` in `native/pdf-signer/src/pdf_sign.c` to support file-based keys (`KEY_SOURCE_FILE`) via `PDF_SIGNER_KEY_PATH` and `PDF_SIGNER_CERT_PATH` (or default `/app/certs/`), while retaining full fallback to PKCS#11 SoftHSM tokens.
   - Configured Dockerfile to automatically generate a dedicated 2048-bit RSA key and self-signed X.509 certificate for `C=SE, O=Nordiska Sparbanken, CN=Nordiska PDF Signer`.
3. **OpenSSL Legacy Provider Compatibility:**
   - ActiveLogin's BankID simulator uses legacy PKCS#12 test certificates encrypted with RC2/3DES. Configured OpenSSL providers (`default` and `legacy`) in `/etc/ssl/openssl.cnf` to ensure BankID and modern CMS signing operate seamlessly together.
4. **Dynamic C API Signing Flag:**
   - In `native/pdf_generator/src/c_api/pdf_generator_c_api.cpp`, enabled signing dynamically: active whenever keys are present (`PDF_SIGNER_KEY_PATH` / `/app/certs/signing_key.pem`) or explicitly overridden via `NORDISKA_PDF_ENABLE_SIGNING=true`.

### Verification:
- Generated account statement PDF via `/api/reports/statement?accountId=1`.
- Extracted and verified the embedded `/Contents` dictionary:
  - **Previous status:** 8,192 zero characters (`0000...0000`).
  - **Current status:** Valid ASN.1 DER CMS `pkcs7-signedData` structure containing `sha256WithRSAEncryption` signed by `C=SE, O=Nordiska Sparbanken, CN=Nordiska PDF Signer`.

