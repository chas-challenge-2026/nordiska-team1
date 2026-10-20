# Kvaldemo Nordiska Sparbanken v2 — Talarmanus: Native & Avslut (2 minuter på slutet)

**Talare:** Jimmy Jordan & Marcus Mandelström  
**Tid:** Exakt 2 minuter (Minut 8:00 – 10:00)  
**Slide:** `slide_2_native.drawio` (Slide 2)  
**Kontext:** Livedemon har precis visat hela kundflödet (BankID, sparmål, överföring, skatteunderlag i inkorgen, FAQ) och Adminportalen. Olle bollar över till Jimmy och Marcus som dyker ner i motorn under huven (Native) och gör presentationens helhetsavslut inför Q&A.

---

## 1. Snabböversikt / Cue-Sheet (Ha bredvid på skärmen)

| Tid | Segment | Talare | Vad pekar du på i Slide 2? | Kärnbudskap / Stödord |
|---|---|---|---|---|
| **00:00–00:45** (45s) | **Native Prestanda & Skalbarhet** | **Jimmy** | C++23 pdf_generator (steg 1–3: Ingest, Layout, Render) | In-memory P/Invoke. simdjson (>155k dok/s), PDF (>44k/s). **Skarpt E2E i Docker på vanlig dator: 10k kunder, 15k konton, 700k transaktioner ~1 min!** (v1 kraschade vid 200). |
| **00:45–01:15** (30s) | **Förtroende & Zero-Trust** | **Jimmy** | Unix Socket ➔ C pdf-signer ➔ PKCS#11 | 1 kund/anrop, all-or-nothing, noll dataläcka. Signerare ser bara 32-byte SHA-256 hash – aldrig kunddata! SoftHSM2/PKCS#11. |
| **01:15–01:35** (20s) | **PAdES-B-T & Standarder** | **Marcus** | TSA Server ➔ PAdES-B-T | Betrodd RFC 3161 TSA-tidsstämpel = PAdES-B-T idag. Förberett för fysisk HSM och PAdES-B-LTA till slutrelease. |
| **01:35–02:00** (25s) | **Helhetsavslut & Överlämning** | **Marcus** | Blick mot kunden / Hela bilden | Knyt ihop säcken: Enkelhet (UI/FAQ), Skalbarhet (Worker/Native), Förtroende (Ledger/PAdES). Bjud in till Q&A & Feed forward! |

---

## 2. Ordagrant Talarmanus (120 sekunder)

### Överlämning från Livedemon
*(Olle avslutar admin-vyn: "Och med det lämnar jag över till Jimmy och Marcus som visar hur motorn under huven fungerar och knyter ihop säcken!")*

---

### [00:00 – 00:45] Jimmy — Native Prestanda & Skalbarhet
*(Dela skärmen och visa **Slide 2**: `slide_2_native.drawio`. Peka på **C++23 pdf_generator**, steg 1–3: Ingest ➔ Layout ➔ Render)*

> "Tack Olle! När ni nyss såg kunden beställa sitt skatteunderlag och få det i inkorgen direkt, så var det vår Native-pipeline som jobbade bakom kulisserna.  
> 
> Vår .NET-worker anropar C++23-motorn in-memory via ett strikt C89 ABI och P/Invoke – helt utan långsam disk-I/O emellan. Med SIMD-accelererad JSON-parsing läser vi över **155 000 dokument i sekunden** och vår interna motor renderar över **44 000 PDF:er per sekund**.  
> 
> Vi har också benchat hela den skarpa produkten deployad i Docker på helt vanliga datorer: **10 000 kunder, 15 000 konton och 700 000 transaktioner** – från att jobben stageas i databasen till att alla PDF:er är genererade och skrivna – tar **cirka en minut end-to-end**.  
> 
> V1:ans system låste sig och tog timeout redan vid 200 transaktioner; vi kör alltså 700 000 transaktioner på en minut."

---

### [00:45 – 01:15] Jimmy — Förtroende, Zero-Trust & Invarianter
*(Visuell cue: Peka på **Unix Socket ➔ C pdf-signer ➔ PKCS#11**)*

> "Men för en bank räcker inte bara prestanda – det krävs absolut förtroende och regulatorisk trygghet. Därför har vi byggt in tre hårda säkerhetsinvarianter:  
> 
> För det första: **Atomärt kundkontrakt**. Ett anrop hanterar exakt en kund. Inget delat tillstånd, och minsta fel avbryter hela batchen. Det gör det strukturellt omöjligt att kunddata läcker mellan PDF:er.  
> 
> För det andra: **Zero-Trust över Unix-socket**. Vår isolerade C-signerare tar endast emot en 32-byte SHA-256-hash. Signeringsprocessen ser alltså aldrig själva kunddatan.  
> 
> För det tredje: Den privata nyckeln är hårdvaruisolerad i SoftHSM2 via **PKCS#11** och lämnar aldrig säker miljö."

---

### [01:15 – 01:35] Marcus — PAdES-B-T & Standarder
*(Visuell cue: Peka på **TSA Server ➔ PAdES-B-T**)*

> "Tillsammans med en kryptografisk RFC 3161-tidsstämpel från vår TSA-server uppnår vi **PAdES-B-T-nivå** redan idag – vilket ger juridiskt bindande och verifierbara bankdokument.  
> 
> Arkitekturen med PKCS#11 gör att vi kan byta till en fysisk HSM i produktion utan att ändra en rad kod, och vi har förberett banan för långtidsvalidering, **PAdES-B-LTA**, till slutreleasen."

---

### [01:35 – 02:00] Marcus — Helhetsavslut & Överlämning till Q&A
*(Visuell cue: Blick mot kunden / kameran)*

> "För att knyta ihop hela demon:  
> Ni har sett **Enkelhet** för kunden genom smidig BankID-inloggning, målstyrt sparande och direkt hjälp i FAQ:n.  
> Ni har sett **Skalbarhet** genom vår asynkrona kö och den blixtsnabba Native-motorn.  
> Och ni har sett **Förtroende** genom immutabelt ledger i databasen och säkert signerade handlingar.  
> 
> Vi är Team 1 – varmt tack för att ni lyssnade, och vi ser mycket fram emot era frågor och er feed forward!"

---

## 3. Stödordsvariant (För att tala fritt)

* **Jimmy (00:00–00:45) Prestanda & Docker E2E:**  
  – Skatteunderlaget nyss skapades av Native-motorn.  
  – In-memory P/Invoke via C89 ABI, noll disk-I/O.  
  – simdjson: >155 000 dok/s, rendering: >44 000 PDF/s.  
  – **Skarpt Docker E2E-test på vanlig laptop:** 10 000 kunder, 15 000 konton, 700 000 transaktioner på **~1 minut** (stagea jobb, köa, rendera, skriva PDF).  
  – Jämförelse: v1 kraschade vid 200 transaktioner.  
* **Jimmy (00:45–01:15) Förtroende & Zero-Trust:**  
  – Atomärt kundkontrakt: 1 kund/anrop, all-or-nothing, noll dataläckage.  
  – Zero-trust över Unix-socket: Signeraren ser bara 32-byte hash – aldrig kunddata!  
  – PKCS#11 / SoftHSM2: Nyckeln lämnar aldrig HSM.  
* **Marcus (01:15–01:35) PAdES-B-T:**  
  – RFC 3161 TSA-stämpel = PAdES-B-T idag.  
  – Förberett för fysisk HSM och PAdES-B-LTA till v2-slutleveransen.  
* **Marcus (01:35–02:00) Avslutning:**  
  – Ledorden summerade: Enkelhet (kundresa), Skalbarhet (backend/native), Förtroende (ledger/signering).  
  – Tacka och öppna för Q&A och feed forward!

---

## 4. Tips inför live-bytet

1. **Vem delar skärmen?**  
   När Olle säger *"över till Jimmy och Marcus"*, delar Jimmy eller Marcus upp **Slide 2 i fullskärm** direkt. Ha fliken eller bilden öppen och redo i bakgrunden så att bytet tar max 2 sekunder.
2. **Peka medan Jimmy talar:**  
   När Jimmy nämner "steg 1 till 3", cirkla med muspekaren runt `pdf_generator`. När han nämner "Unix-socket", peka på pilen ner mot `pdf-signer`.
3. **Överlämning till Q&A:**  
   När Marcus säger *"vi ser fram emot era frågor och er feed forward"*, stäng skärmdelningen eller låt Slide 2 ligga kvar i bakgrunden så att kundens blick återgår till teamets kameror under de 6 minuterna Q&A.
