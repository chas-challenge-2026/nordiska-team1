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

## 7. Dynamic Linker & Shared Library Fix (`Dockerfile`)

### What I changed and why:
- **File:** `Dockerfile` (Stage 4 `final`)
  - Added `libsimdjson14=3.0.1-1` to the runtime `apt-get install`.
  - Added `COPY --from=native-builder /usr/local/lib64/libstdc++.so.6* /usr/local/lib/` before running `ldconfig`.
  - Why: The 500 error was caused by a `System.DllNotFoundException` at the P/Invoke boundary because `libnordiska_pdf_generator_c_api.so` could not resolve `libsimdjson.so.14` and required `GLIBCXX_3.4.31` (from GCC 13's `libstdc++6`), which wasn't in the default Debian 12 base image. With these additions, all native dependencies are resolved, verified via `ldd`, and live PDF/ZIP generation returns 200 OK.






