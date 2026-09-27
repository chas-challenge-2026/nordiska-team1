# BankID Test Environment & Developer Guide

This document provides a developer guide for using BankID in Nordiska Sparbanken. The system integrates the official BankID Relying Party v6.0 test environment with dynamic QR code authentication and same-device autostart.

---

## Official Documentation & Setup

Follow the official BankID guides to set up your test client (Mobile or Desktop) and issue test certificates:
- **BankID Test Environment Guide:** https://developers.bankid.com/test-portal/bankid-for-test
- **BankID Test Portal (Issue Test Certificates):** https://developers.bankid.com/test-portal/testing

---

## Project Certificates

The mTLS certificates used by the backend to communicate with BankID test servers are located in the repository:
- **Backend Client Certificate (mTLS):** [`FPTestcert5_20240610.p12`](../../backend/src/Nordiska.FrontendApi/Certificates/FPTestcert5_20240610.p12)
- **Certificates Directory:** [`Certificates/`](../../backend/src/Nordiska.FrontendApi/Certificates/)
- **Certificate Documentation:** [`Certificates/README.md`](../../backend/src/Nordiska.FrontendApi/Certificates/README.md)

---

## Active Test Users in the System

| Name | Personal Identity Number (SSN) | Email | Role / Type | Description |
| :--- | :--- | :--- | :--- | :--- |
| **Jesper Adminsson** | `20050503-2383` | `admin@nordiska.se` | **Admin** | Administrator account with full management privileges. |
| **Anna Smith** | `19820211-6050` | `anna@example.com` | **Customer** | Standard customer for testing. Pre-seeded accounts `NOR-100001` & `NOR-100002`. |
| **Erik Svensson** | `19790314-2380` | `erik@example.com` | **Customer** | Standard customer for testing. Pre-seeded accounts `NOR-200001` & `NOR-200002`. |

*Note: You can issue your own test certificates in the [BankID Test Portal](https://developers.bankid.com/test-portal/testing) and register as a new customer via the **"Bli kund"** page (`/register`).*

---

## Running Locally

### 1. Start Services
```powershell
# 1. Start PostgreSQL database
cd "repo/infra"
docker compose up -d db

# 2. Start Backend (.NET Web API)
cd "repo/backend/src/Nordiska.FrontendApi"
dotnet run

# 3. Start Frontend (React)
cd "repo/frontend"
npm run dev
```

### 2. Log in with BankID
1. Navigate to **`http://localhost:5173/login`**.
2. Enter personal identity number (e.g. `20050503-2383` for Admin).
3. Click **"Starta BankID"**.
4. Authenticate by either:
   - Scanning the dynamic QR code with your BankID mobile app configured for `kundtest`.
   - Clicking *"Öppna BankID på denna enhet"* to launch BankID Security Application on desktop.

---

## Alternative Login Methods

For quick development without configuring test certificates:

### Option A: Simulated BankID Mode
Set the environment to `Simulated` in `backend/src/Nordiska.FrontendApi/appsettings.Development.json`:
```json
"ActiveLogin": {
  "BankId": {
    "Environment": "Simulated"
  }
}
```
Enter any personal identity number (e.g. `19820211-6050`) on the login page. Authentication completes automatically in the browser.

### Option B: Email & Password
Use the email login form at `/login`:
- Email: `anna@example.com` (or `admin@nordiska.se`)

---

## Technical Architecture

### Backend (.NET Web API)
- **Library:** `ActiveLogin.Authentication.BankId.AspNetCore` (v11.1.3).
- **mTLS:** `Certificates/FPTestcert5_20240610.p12` loaded when `Environment == "Test"` for mutual TLS against `https://appapi2.test.bankid.com/rp/v6.0/`.
- **Endpoints:**
  - `POST /api/auth/bankid/initiate`: Initiates BankID order and returns QR parameters.
  - `POST /api/auth/bankid/collect`: Polls order status and issues HttpOnly JWT cookie (`access_token`).
  - `POST /api/auth/register`: Registers new customer accounts.

### Frontend (React 18)
- **Dynamic QR Code (`BankIdQrCode.tsx`):** Recalculates `bankid.{qrStartToken}.{elapsedSeconds}.{qrAuthCode}` every second using HMAC-SHA256 via Web Crypto API.
- **Same-Device Launch:** Uses `bankid:///?autostarttoken=...` protocol handler.

---

## Troubleshooting

- **Error code 10026 / Order cancelled:** Ensure desktop BankID configuration file `%appdata%\BankID\Config\CavaServerSelector.txt` contains `kundtest`.
- **BankID missing (10036):** The entered personal number does not match the certificate installed on the device.
- **401 Unauthorized:** The user is not in the database. Register first via the **"Bli kund"** page (`/register`).
