# Kvaldemo Nordiska Sparbanken v2 — Talarmanus (Minut 1 & 2)
**Talare:** Marcus Mandelström & Jimmy Jordan  
**Tid:** Exakt 2 minuter (00:00 – 02:00)  
**Slides:** `slide_1_systemarkitektur.drawio` & `slide_2_native.drawio`

---

## 1. Snabböversikt / Cue-Sheet (Ha bredvid på skärmen)

| Tid | Slide | Talare | Vad pekar du på? | Kärnbudskap / Stödord |
|---|---|---|---|---|
| **00:00–00:20** | **Slide 1** | **Marcus** | Hela skärmen | Välkomna, Team 1. Vad vi ska visa (BankID, sparkonto/sparmål, överföring, skatteunderlag, FAQ, admin). |
| **00:20–00:40** | **Slide 1** | **Marcus** | SPA ➔ API-moduler ➔ DB | **Enkelhet & Skalbarhet:** Bröt monoliten. React 18, .NET 8 (4 isolerade moduler), Postgres med immutabelt ledger. |
| **00:40–01:00** | **Slide 1** | **Jimmy** | DB-kö ➔ Worker ➔ Native | **Förtroende & Frikoppling:** API svarar direkt (<50ms). Worker leasar jobb ur Postgres, skickar till Native. |
| **01:00–01:25** | **Slide 2** *(Byt slide!)* | **Jimmy** | C++ pdf_generator (steg 1–3) | **Skalbarhet & Benchmarks:** In-memory P/Invoke utan disk-I/O. simdjson (>155k dok/s), PDF (>44k/s). 50k kunder < 4 sek! |
| **01:25–01:45** | **Slide 2** | **Jimmy** | Socket ➔ C-signer ➔ PKCS#11 | **Förtroende & Zero-Trust:** 1 kund/anrop, all-or-nothing. Signerare ser bara 32-byte hash – aldrig kunddata! SoftHSM2/PKCS#11. |
| **01:45–02:00** | **Slide 2** | **Marcus** | PAdES-B-T ➔ TSA | PAdES-B-T med betrodd TSA-tidsstämpel idag. Förberett för fysisk HSM & B-LTA. Överlämning till Joco! |

---

## 2. Ordagrant Talarmanus (120 sekunder)

### MINUT 1 — Övergripande systemarkitektur
*(Visar **Slide 1**: `slide_1_systemarkitektur.drawio`)*

#### [00:00 – 00:20] Marcus (Hälsning & Vad demon visar)
> "Hej och varmt välkomna! Vi är Team 1.  
> 
> Idag kommer vi visa en komplett kund- och bankresa: hur en kund loggar in med BankID, öppnar ett sparkonto med sparmål, flyttar pengar, tar ut ett digitalt signerat skatteunderlag direkt i inkorgen, söker i FAQ – och hur banken styr allt från vårt admincenter."

#### [00:20 – 00:40] Marcus (Ledord: Enkelhet & Skalbarhet)
*(Visuell cue: Peka på React 18 SPA ➔ .NET 8 API-lådan med dess 4 moduler ➔ PostgreSQL)*
> "Vårt arbete har styrts av tre ledord: **Enkelhet**, **Skalbarhet** och **Förtroende**.  
> 
> För att uppnå enkelhet och skalbarhet har vi brutit upp v1-monoliten i tre lager: en modern React 18 SPA, ett modulärt .NET 8 Web API uppdelat i fyra strikt isolerade domäner – Banking, FAQ, Inbox och Reporting – samt en PostgreSQL-databas där saldon säkras av ett oföränderligt, immutabelt ledger."

#### [00:40 – 01:00] Jimmy (Ledord: Förtroende & Frikoppling)
*(Visuell cue: Peka på Rapport-kö i DB ➔ Reporting.Worker ➔ Native C/C++)*
> "För att skapa förtroende och skydda bankens prestanda är rapportgenereringen helt frikopplad från webbservern.  
> 
> När kunden beställer ett skatteunderlag svarar API:et omedelbart. I bakgrunden leasar vår Reporting.Worker jobbet ur databasen och skickar det vidare till vår Native C/C++-pipeline.  
> 
> Låt oss titta på hur den ser ut under huven – vi byter slide."

---

### MINUT 2 — Djupdykning Native (C++23 & C), Benchmarks & Invarianter
*(Byt till **Slide 2**: `slide_2_native.drawio`)*

#### [01:00 – 01:25] Jimmy (Skalbarhet & Benchmarks)
*(Visuell cue: Peka på C++23 pdf_generator, steg 1–3: Ingest, Layout, Render)*
> "Här ser ni vår Native-pipeline. Workern anropar vår C++23-motor in-memory via ett strikt C89-gränssnitt – helt utan långsam disk-I/O emellan.  
> 
> Med SIMD-accelererad JSON-parsing läser vi över **155 000 dokument per sekund**, och vår interna renderingsmotor genererar över **44 000 PDF:er i sekunden**. Det gör att en masskörning för 50 000 kunder vid årsskiftet tar under fyra sekunder."

#### [01:25 – 01:45] Jimmy (Förtroende, Zero-Trust & Invarianter)
*(Visuell cue: Peka på Unix Socket ➔ C pdf-signer ➔ PKCS#11)*
> "För att garantera förtroende har vi byggt in tre hårda invarianter:  
> För det första: **Atomärt kundkontrakt**. Ett anrop hanterar alltid exakt en kund. Inget delat state, ingen risk för dataläckage mellan kunders PDF:er, och minsta fel avbryter hela batchen.  
> För det andra: **Zero-Trust**. Vår isolerade C-signerare körs över en Unix-socket och tar endast emot en 32-byte SHA-256-hash – signeraren ser alltså aldrig själva kunddatan.  
> För det tredje: Den privata nyckeln lämnar aldrig säker miljö utan skyddas via **SoftHSM2 och PKCS#11**."

#### [01:45 – 02:00] Marcus (PAdES-B-T & Överlämning till Livedemo)
*(Visuell cue: Peka på TSA Server ➔ PAdES-B-T)*
> "Tillsammans med en kryptografisk RFC 3161-tidsstämpel uppnår vi **PAdES-B-T-nivå** redan idag. Arkitekturen är dessutom direkt förberedd för fysisk HSM och långtidsvalidering, PAdES-B-LTA, till slutreleasen.  
> 
> Detta ger Nordiska Sparbanken en blixtsnabb och juridiskt säker grund. Nu ska vi se hur detta ser ut i händerna på kunden – jag lämnar över skärmen till Joco för livedemon!"

---

## 3. Stödordsvariant (För att tala fritt)

### Minut 1: Slide 1 (Arkitektur)
* **Marcus (00–20s):** Välkomna, Team 1. Kundresa: BankID, konto/sparmål, överföring, skatteunderlag, FAQ, admin.
* **Marcus (20–40s):** Enkelhet & Skalbarhet. React 18 SPA + .NET 8 Web API (4 moduler) + PostgreSQL (immutabelt ledger).
* **Jimmy (40–60s):** Förtroende & Asynkron kö. Direkt svar till kund (<50ms). Worker leasar jobb, delegerar till Native. Byt slide!

### Minut 2: Slide 2 (Native & Invarianter)
* **Jimmy (60–85s):** In-memory C89 / P/Invoke utan disk-I/O. simdjson >155 000 dok/s, rendering >44 000 PDF/s. 50 000 kunder < 4 sek.
* **Jimmy (85–105s):** Atomärt kundkontrakt (all-or-nothing, noll dataläcka). C pdf-signer över Unix socket ser bara 32-byte SHA-256 hash. SoftHSM2/PKCS#11.
* **Marcus (105–120s):** PAdES-B-T med betrodd TSA idag. Förberett för fysisk HSM + B-LTA. Över till Joco för live!
