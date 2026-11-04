# ADR: Native PDF Generator – Arkitektur, Säkerhet och Prestanda

**Författare:** Jimmy Jordan (Native Systems & Performance)  
**Status:** Accepted / Implemented  
**Datum:** 2026-11-04  
**Kontext:** Chas Challenge 2026 – Nordiska Sparbanken v2  

---

## 1. Kontext och problemställning

I Nordiska Sparbanken v1 hanterades kundrapporter (kontoutdrag och skatteunderlag) manuellt eller via långsamma batchprocesser, vilket ledde till postförseningar och över 500 supportärenden i veckan. 

För v2 behövde banken ett högpresterande, automatiserat system för on-demand och massgenerering av signerade PDF-dokument som:
1. **Garanterar banksekretess och dataskydd (PII):** Känsliga kontouppgifter och personnummer får inte spridas över oskyddade nätverk, exponeras för onödiga systemkomponenter eller läcka till applikationsloggar.
2. **Klarar massgenerering under hårda prestandakrav:** Årlig skatteunderlagsgenerering för tiotusentals kunder måste kunna slutföras på minuter snarare än timmar utan att överbelasta bankens infrastruktur.
3. **Producerar juridiskt och kryptografiskt verifierbara dokument:** Stöd för standardiserade digitala signaturer (PAdES/CMS) och tidsstämplar (RFC 3161).
4. **Är framtidssäkert och utbyggbart:** Nya dokumenttyper och optimerade renderingsmotorer ska kunna läggas till utan att bryta integrationskontraktet med .NET-backend.

---

## 2. Arkitekturbeslut

### Beslut 1: In-process C++23-bibliotek med C89-ABI (istället för extern HTTP-mikrotjänst)

* **Beslut:** PDF-generatorn byggs som ett fristående native C++23-bibliotek (`libnordiska_pdf_generator_c_api.so`) som laddas in-process av .NET Reporting Worker via P/Invoke.
* **Motivering:**
  - **Säkerhet:** Kunddata skickas direkt i minnet mellan worker och bibliotek. Vi slipper temporära filer på disk och okrypterad nätverkstrafik mellan containrar.
  - **Prestanda:** Eliminerar all nätverkslatens och HTTP/REST-serialisering. JSON lånas synkront och färdiga PDF-bytes levereras via callback, varefter minnet frigörs omedelbart.
* **Avfärdade alternativ:**
  - *Headless browser (Puppeteer / Chromium):* För hög minnesförbrukning, trög uppstart och stor attackyta.
  - *Fristående Docker REST-mikrotjänst:* Avfärdades efter CTO-workshopen med Victor Axelsson, då skyddet av kundernas bankdata prioriterades högre än processisolering via container.

### Beslut 2: Tre-stegs pipeline med modulär layout-mellanrepresentation

* **Beslut:** Arkitekturen delas strikt upp i tre oberoende faser bakom C++-gränssnitt:
  1. **Ingestion (`JsonIngestor`):** Validerar och tolkar rå UTF-8 JSON. Standardtolken använder `simdjson` för maximal hastighet (>155 000 dok/s) med noll-kopiering av textsträngar; `nlohmann-json` finns som kompatibilitetsfallback.
  2. **Layout (`LayoutBuilder`):** Bestämmer koordinater, typografi, tabellflöden och sidbrytning, och producerar en abstrakt mellanrepresentation (`DocumentLayout`). Layoutsteget känner inte till hur PDF-bytes serialiseras.
  3. **Rendering (`PdfEngine`):** Konverterar mellanrepresentationen till binära PDF-dataströmmar.
* **Motivering:** Frikopplar layout och affärsregler från PDF-formatets interna detaljer. Gör det enkelt att skapa nya rapporttyper och byta ut renderingsmotorer utan att påverka befintlig kod.

### Beslut 3: Dedikerad egen PDF-motor (Native) som komplement till Libharu

* **Beslut:** Utvecklade en specialanpassad PDF 1.4-motor (`NativePdfEngine`) vid sidan av standardmotorn Libharu.
* **Motivering:**
  - Standardtypsnitt (Core-14 Helvetica/Helvetica-Bold) används utan inbäddade typsnittsfiler, vilket minimerar filstorleken.
  - Formatering av koordinater och siffror sker med noll allokeringar via `std::to_chars`.
  - Förkomprimerade rasterbilder bäddas in direkt utan omkomprimering.
* **Prestandavinster uppmätta i benchmark:**
  - **Hastighet:** 1,4–1,8× snabbare vid ren textlayout (0,21 ms/dok mot Harus 0,36 ms/dok; över 44 000 dok/s) och upp till 5× snabbare vid profilerade dokument med logotyp.
  - **Filstorlek:** 33 % till 54 % mindre filer (1,67 KB mot Harus 3,62 KB i syntetisk masskörning).

### Beslut 4: Zero-Knowledge digital signering via Unix Domain Socket

* **Beslut:** PDF-generatorn utför inte kryptografisk signering själv utan förbereder PDF:en med en reserverad signeringslucka (`/ByteRange` och `/Contents`) och beräknar SHA-256 över de relevanta byten. Endast denna digest skickas över en Unix domain socket till Marcus signerartjänst.
* **Motivering:**
  - Strikt princip om minsta privilegium: PDF-generatorn behöver ingen tillgång till privata nycklar eller HSM/PIN-koder.
  - Signerartjänsten ser aldrig kunders personnummer, transaktioner eller dokumentinnehåll – enbart en 32-byte SHA-256 hash.

### Beslut 5: Privacy Boundary och säker feldiagnostik

* **Beslut:** All felrapportering och diagnostik kapslas in bakom en strikt integritetsgräns.
* **Motivering:** Vid felaktig indata eller avbruten rendering rapporterar C-gränssnittet fasta felkoder, strukturella JSON-sökvägar eller koordinatpositioner – **aldrig** kunders personuppgifter, namn eller saldobelopp. Detta förhindrar att personuppgifter (PII) hamnar i centraliserade loggsystem (t.ex. Serilog/Elasticsearch).
* **Verifiering:** Ett automatiserat AST-valideringsskript (`check_diagnostic_boundary.py`) körs i CI för att säkerställa att inga otillåtna fält loggas.

---

## 3. Konsekvenser

### Positiva konsekvenser
- **Extrem genomströmning:** Fullständig massgenerering av 25 448 PDF-dokument genomfördes på **52,5 sekunder** i Docker (485 dok/s end-to-end; över 4 100 dok/s i rent minnesläge).
- **Hög säkerhetsnivå:** Noll temporärfillagring på disk, isolerade signeringsnycklar och noll PII i felloggarna.
- **Robust testbarhet:** Modulär uppdelning möjliggör isolerade enhetstester, fuzzing och prestandamätning för varje enskild komponent.

### Negativa konsekvenser / Avvägningar
- **Plattformsberoende:** C++-biblioteket bygger på Linux x86_64 och kräver Docker eller kompatibel Linuxmiljö under Windows/macOS.
- **Teckenstöd begränsat till CP1252/WinAnsi:** För att hålla dokumentstorleken minimal bäddas inga fullständiga TTF/OTF-typsnitt in, vilket begränsar teckenuppsättningen till västeuropeiska språk (å, ä, ö stöds fullt ut).

---

## 4. Sammanfattning och resultat

Den valda arkitekturen har framgångsrikt levererat en bankmässig, säker och extremt snabb PDF-lösning som uppfyller samtliga krav för Chas Challenge 2026. Lösningen är verifierad med 17/17 godkända CTest-testsviter i Debug (med ASan/UBSan) och 100 % felfri generering i storskaliga stresstester.
