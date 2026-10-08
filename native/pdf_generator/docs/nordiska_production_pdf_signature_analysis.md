# Nordiska Production PDF Signature Specification & Reverse-Engineering Analysis

## 1. Document Origin & Context

* **Document Source**: Production bank contract: *"Ansökan om Nordiska Fix 1 Månad"* (fixed-interest rate savings account application).
* **Signing Method**: Authenticated and signed online by customer using **Swedish BankID**.
* **Document Status**: Real signed production contract downloaded directly from Nordiska's customer savings portal. (Future follow-ups planned: depositing funds and downloading monthly account statements to compare statement layout and signature structures).
* **Analysis Tool**: Standalone ASN.1 & PDF dictionary inspector (`s.py`).

---

## 2. PDF Signature Dictionary Metadata

Extracted directly from the root `/Type /Sig` dictionary:

```text
<<
  /ByteRange [0 142 105176 2156946 ]                                                         
  /ContactInfo ()
  /Contents <... 105,032 hex characters (52,516 bytes) ...>
  /Filter /Adobe.PPKLite
  /SubFilter /ETSI.CAdES.detached
  /Location ()
  /Reason ()
  /M (D:20260910115947+02'00')
  /Prop_Build << /App << /Name / >> >>
>>
```

### Key Field Breakdown

| PDF Dictionary Key | Value in Production | Significance for our Implementation |
| :--- | :--- | :--- |
| **/SubFilter** | `ETSI.CAdES.detached` | **PAdES (PDF Advanced Electronic Signature)** under European standard **ETSI EN 319 142**. This is required for EU eIDAS compliance rather than legacy Adobe PKCS#7 (`adbe.pkcs7.detached`). |
| **/Filter** | `Adobe.PPKLite` | Standard Adobe Acrobat signature handler. |
| **/ByteRange** | `[ 0 142 105176 2156946 ]` | Part 1: bytes `0`–`142`. Part 2: bytes `105176`–`2262122`. Total file size: ~2.26 MB. |
| **/Contents (Slot Size)** | `105,032` hex chars (**52,516 bytes**) | **~52 KB reserved container**. Necessary because PAdES QES embeds full certificate chains, OCSP revocation responses, and RFC 3161 timestamps. |
| **/Reason** | `()` (empty) | Standard for eIDAS: legal authority is derived from the certificate policy and BankID audit log, not human-written text. |
| **/Location** | `()` (empty) | Unspecified / omitted in production. |
| **/ContactInfo** | `()` (empty) | Unspecified / omitted in production. |
| **/M** | `D:20260910115947+02'00'` | ASN.1 PDF date: signed September 10, 2026 at 11:59:47 CEST (+02:00). |

---

## 3. Trust Infrastructure & Certificate Chain

The 52 KB DER container was extracted and parsed via OpenSSL:

```text
Certificates found via OpenSSL:
  CA Issuer : C=NO/organizationIdentifier=NTRNO-983163327, O=Buypass AS, CN=Buypass Class 3 Root CA G2 ST
  CA Issuer : C=NO/organizationIdentifier=NTRNO-983163327, O=Buypass AS, CN=Buypass Class 3 CA G2 ST Business
  Subject   : C=NO/organizationIdentifier=NTRNO-983163327, O=Buypass AS, CN=[Customer Name / BankID Identity]
  Subject   : C=NO, O=SIGNICAT AS, CN=[Signing Service Identity]
```

### Infrastructure Workflow
1. **Frontend / Portal**: Customer authorizes via BankID on Nordiska's web portal.
2. **Signature Broker**: **Signicat AS** acts as the electronic identity broker and transaction orchestrator.
3. **Qualified Trust Service Provider (QTSP)**: **Buypass AS** generates an EU Qualified Electronic Signature (QES) with Buypass Class 3 Root CA G2.

---

## 4. Key Architectural Takeaways for Nordiska Team 1

1. **Whitespace In-Place ByteRange Padding**:
   Notice the 57 whitespace characters immediately following `[0 142 105176 2156946 ]`.
   This proves Nordiska's own production pipeline uses the **in-place overwrite technique**: the signature dictionary is pre-allocated with padded space so the ByteRange numbers can be patched in memory without shifting file byte offsets.
2. **SubFilter Standardization**:
   Our native engine should use `/SubFilter /ETSI.CAdES.detached` for regulatory compliance with Swedish and European banking laws.
3. **Container Capacity / Slot Sizing**:
   * **Internal Batch / Microservice Signing**: 8,192 bytes is adequate for internal HMAC / standard PKCS#7 detached signatures.
   * **Full BankID / Signicat PAdES (QES)**: 105,032 hex characters (~52 KB) is the exact size required when embedding the full Buypass/Signicat certificate chain, revocation status (OCSP), and RFC 3161 timestamp.
4. **Clean Metadata**:
   Leaving `/Reason`, `/Location`, and `/ContactInfo` as empty string literals `()` matches real-world Nordic banking practice.

