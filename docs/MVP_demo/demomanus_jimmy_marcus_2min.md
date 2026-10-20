# Kvaldemo Nordiska Sparbanken v2 — Talarmanus: Native & Avslut (2 minuter på slutet)

**Talare:** Jimmy Jordan & Marcus Mandelström  
**Tid:** Exakt 2 minuter (Minut 8:00 – 10:00)  
**Slide:** `slide_2_native.drawio` (Slide 2)  
**Kontext:** Livedemon har precis visat hela kundflödet (BankID, sparmål, överföring, skatteunderlag i inkorgen, FAQ) och Adminportalen. Olle bollar över till Jimmy och Marcus som dyker ner i motorn under huven (Native) och gör presentationens helhetsavslut inför Q&A.

---

## 1. Snabböversikt / Cue-Sheet (Ha bredvid på skärmen)

| Tid | Segment | Talare | Vad pekar du på i Slide 2? | Kärnbudskap / Stödord |
|---|---|---|---|---|
| **00:00–00:40** (40s) | **Native Prestanda & Skalbarhet** | **Jimmy** | C++23 pdf_generator (steg 1–3: Ingest, Layout, Render) | In-memory via callback, noll hårddiskanvändning. **Prestanda på vanlig dator: 10k kunder, 15k konton, 700k transaktioner sparade i DB på ca 1 min!** |
| **00:40–01:05** (25s) | **Förtroende & Zero-Trust** | **Jimmy** | Unix Socket ➔ C pdf-signer | 1 kund/anrop, all-or-nothing, noll dataläcka. Signerare tar bara emot en anonym hash – ser aldrig kunddata! |
| **01:05–01:35** (30s) | **Signering, PKCS#11 & HSM** | **Marcus** | C pdf-signer ➔ PKCS#11 ➔ TSA Server | Fristående signering skild från PDF-generering. PKCS#11 + tidsstämpling, isolerad nyckelhantering, redo för riktig HSM. Modulär lösning. |
| **01:35–02:00** (25s) | **Helhetsavslut & Överlämning** | **Marcus** | Blick mot kunden / Hela bilden | Knyt ihop säcken: Enkelhet (kundupplevelse), Skalbarhet (arkitektur/asynkron/native), Förtroende (DB-roller, säker data, signering). Bjud in till Q&A & Feed forward! |

---

## 2. Ordagrant Talarmanus (120 sekunder)

### Överlämning från Livedemon
*(Olle avslutar admin-vyn: "Och med det lämnar jag över till Jimmy och Marcus som visar hur motorn under huven fungerar och knyter ihop säcken!")*

---

### [00:00 – 00:40] Jimmy — Native Prestanda & Skalbarhet
*(Dela skärmen och visa **Slide 2**: `slide_2_native.drawio`. Peka på **C++23 pdf_generator**, steg 1–3: Ingest ➔ Layout ➔ Render)*

> "Tack Olle! När ni nyss såg kunden beställa sitt skatteunderlag och få det i inkorgen direkt, så var det vår Native-pipeline som jobbade bakom kulisserna.  
> 
> Den klarar även att batch-skapa och spara signerade dokument till databasen för 10000 kunder på en minut.
> Totalt 15000 konton med 700 tusen transaktioner. Och detta på en helt vanlig dator.

---

### [00:40 – 01:05] Jimmy — Förtroende, Zero-Trust & Invarianter
*(Visuell cue: Peka på **Unix Socket ➔ C pdf-signer**)*

> "Men för en bank krävs också absolut förtroende. Därför har vi byggt in hårda säkerhetsinvarianter:  
> 
> För det första: Ett anrop hanterar exakt en kund. Inget delat tillstånd, och minsta fel avbryter hela batchen. Det gör det strukturellt omöjligt att kunddata läcker mellan PDF:er.  
> 
> För det andra: Vår isolerade C-signerare tar endast emot en anonym hash. Signeringsprocessen kan alltså aldrig se själva kunddatan."

---

### [01:05 – 01:35] Marcus — Signering, PKCS#11 & Moduläritet
*(Visuell cue: Peka på **PKCS#11 ➔ TSA Server**)*

>Vi har byggt signeringen som en fristående del av PDF-flödet, där nyckelhantering, signering och tidsstämpling är tydligt separerade från resten av dokumenthanteringen.
>Det gör lösningen enkel att vidareutveckla eller förändra utan att påverka övriga delar.
>Tidsstämplingen gör att dokumenten kan verifieras på ett tillförlitligt sätt även långt efter att de skapats, och vi har samtidigt byggt grunden för att kunna utöka lösningen med ännu starkare stöd för långtidsvalidering framåt.
---

### [01:35 – 02:00] Marcus — Helhetsavslut & Överlämning till Q&A
*(Visuell cue: Blick mot kunden / kameran)*

>För att knyta ihop demon har ni fått se hur enkelhet, skalbarhet och förtroende genomsyrar lösningen. Enkelhet i kundupplevelsen med BankID, målstyrt sparande och snabb hjälp via FAQ:n. 
>
>Skalbarhet genom asynkron hantering, tydligt separerade komponenter och en native-del som hanterar hög belastning effektivt. Och förtroende genom tydliga databasroller, säker hantering av >kunddata genom hela flödet och signerade, tidsstämplade dokument.
>
>Vi är Nordiska Team 1. Stort tack för att ni lyssnade — vi ser fram emot era frågor och er feed forward.

---

## 3. Stödordsvariant (För att tala fritt)

* **Jimmy (00:00–00:40) Prestanda & Docker E2E:**  
  – Skatteunderlaget nyss skapades av Native-motorn.  
  – In-memory anrop via callback, noll hårddiskanvändning.  
  – En kunds alla dokument per anrop, returneras signerade och klara.  
  – **Prestanda på vanlig dator:** 10 000 kunder, 15 000 konton, 700 000 transaktioner sparade i DB på ca 1 minut.  
* **Jimmy (00:40–01:05) Förtroende & Zero-Trust:**  
  – 1 kund/anrop, all-or-nothing, noll dataläckage.  
  – Zero-trust: Signeraren tar bara emot en anonym hash – ser aldrig kunddata!  
* **Marcus (01:05–01:35) Signering, PKCS#11 & Moduläritet:**  
  – Fristående signering skild från PDF-generering: enklare underhåll och vidareutveckling.  
  – PKCS#11 + tidsstämpling: isolerad nyckelhantering, koppla mot riktigt HSM utan ombyggnad.  
  – Modulär lösning: varje del kan skalas, bytas ut eller vidareutvecklas separat.  
* **Marcus (01:35–02:00) Avslutning:**  
  – Ledorden summerade: Enkelhet (kundupplevelse), Skalbarhet (arkitektur/asynkron/native), Förtroende (DB-roller, säker data, signerade/tidsstämplade dok).  
  – Tacka och öppna för Q&A och feed forward!

---

## 4. Tips inför live-bytet

1. **Vem delar skärmen?**  
   När Olle säger *"över till Jimmy och Marcus"*, delar Jimmy eller Marcus upp **Slide 2 i fullskärm** direkt. Ha fliken eller bilden öppen och redo i bakgrunden så att bytet tar max 2 sekunder.
2. **Peka medan Jimmy och Marcus talar:**  
   När Jimmy nämner "steg 1 till 3", cirkla med muspekaren runt `pdf_generator`. När han nämner "Unix-socket", peka på pilen ner mot `pdf-signer`. När Marcus pratar om PKCS#11 och HSM, peka på nyckeln och TSA-kopplingen.
3. **Överlämning till Q&A:**  
   När Marcus säger *"vi ser fram emot era frågor och er feed forward"*, stäng skärmdelningen eller låt Slide 2 ligga kvar i bakgrunden så att kundens blick återgår till teamets kameror under de 6 minuterna Q&A.
