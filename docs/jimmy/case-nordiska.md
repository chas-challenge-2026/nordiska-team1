# Nordiska: Självserviceportal för sparande

**Chas Academy – Chas Extended Challenge 2026**

## Kund

- **Kund:** Nordiska
- **Segment:** Sparkonton, privatkunder och digital självservice
- **Backend i v1:** .NET

## Bakgrund

Nordiska är en ung bank med banktillstånd från slutet av 2024. Banken ser sig lika mycket som ett techbolag: banken är inte produkten, utan infrastrukturen.

När fler kunder möter Nordiska direkt blir kundupplevelsen själva produkten. I dag hanterar Nordiska ungefär 500 ärenden i veckan. Många är enkla, återkommande frågor om ränta, uttag, årsbesked och kontoutdrag – information som redan finns, men där vägen till informationen är för lång.

Nordiska vill inte skala genom fler människor, utan genom teknik. Tre ledord för projektet är:

- enkelhet
- skalbarhet
- förtroende

> ”Det handlar inte om dokument. Det handlar inte om en portal. Det handlar om hur vi bygger en bank som kan växa utan att ta med sig gamla arbetssätt.”
>
> — Kajsa Dymling, Engineering Manager, Nordiska

## Kundens problem

Kunder vill kunna sköta vanliga ärenden själva, men tvingas ofta kontakta kundtjänst. Kundtjänsten belastas av återkommande frågor om:

- konton
- räntor
- uttag
- rapporter
- villkor

Problemet är alltså inte ett enskilt dokument. Informationen finns redan, men friktionen mellan kunden och informationen uppstår om och om igen.

## Vad kunden vill uppnå

Nordiska vill ha en modern självserviceportal där kunder kan:

- hantera konton och sparprodukter digitalt
- göra insättningar och uttag
- generera skatterapporter och kontoutdrag automatiskt
- få svar via en regelstyrd FAQ-assistent

Målet är att kundtjänsten ska kunna lägga mer tid på rådgivning och komplexa ärenden.

Eftersom lösningen hanterar pengar måste den kännas:

- trygg
- tydlig
- enkel

## Uppdraget: från v1 till v2

Teamet tar över en v1 där:

- saldon uppdateras med direkta `UPDATE`-satser, vilket kan orsaka race conditions
- skatterapporter genereras inline tills requesten riskerar att timea ut
- FAQ-assistenten använder en enkel keyword-grep och ofta svarar fel

Uppdraget är att bygga en skalbar och spårbar v2 med:

- säkra transaktioner
- batchvänlig rapportgenerering
- en FAQ som träffar rätt

## MVP-flöde för v2

MVP:n ska klara följande flöde:

1. Användaren loggar in med en mockad BankID-klient.
2. Användaren ser sina sparkonton.
3. Användaren gör en insättning eller ett uttag.
4. Användaren genererar en skatterapport i PDF-format.
5. Användaren söker svar i FAQ-portalen.

BankID är en mockad klient som främst behöver stödja happy path. FAQ:n ska bygga på regelstyrd sökning mot fördefinierade frågor och svar.

## Funktioner att arbeta med

- Säker inloggning via BankID-mock.
- Åtkomst till konton, transaktioner och dokument.
- Kontodashboard med sparkonton, saldo, räntesats, historiska transaktioner och kontostatus.
- Insättningar och uttag med status i realtid.
- Kontohantering: öppna och avsluta sparkonto, uppdatera kontaktuppgifter och se villkor.
- Transaktionshistorik över insättningar, uttag, ränteutbetalningar och kontohändelser.
- Skatterapporter: automatiskt genererade årsbesked, ränteunderlag och skatterapporter som PDF.
- FAQ-assistent mot en kontrollerad FAQ-databas, till exempel ”Hur gör jag ett uttag?” och ”När betalas räntan ut?”.
- Notifieringar.
- Audit-logg över viktiga händelser.

## C/C++-öppningen

De två native-modulerna hör hemma i den prestandakritiska PDF-hanteringen:

1. En PDF-generator för skatterapporter och masskörning vid årets slut.
2. PDF-signering med hash och metadata för audit och spårbarhet.

Teamet ska inte bygga allt. En junior konsult förstår problemet, prioriterar det som skapar störst kundvärde, avgränsar resten och kan motivera varför.

## Teknisk fördjupning

Den tekniska kravställningen beskriver v1-stacken, målen för v2 och datamodellen. Den ska användas som referens under projektet.

### v1-stacken som teamet tar över

| Område | Så ser v1 ut |
|---|---|
| Backend | .NET 6, ADO.NET direkt, Razor Pages och ingen tydlig API-yta |
| Transaktioner | Balansen uppdateras med direkta `UPDATE`-satser, vilket kan ge race conditions vid samtidiga transaktioner |
| Skatterapport | Genereras inline i request-loopen med iTextSharp enligt casebeskrivningen; requesten riskerar timeout vid batchkörning |
| FAQ-assistent | Enkel keyword-grep mot en hårdkodad lista; returnerar ofta fel eller slumpmässiga svar |
| Notifieringar | Mailutskick direkt från controllern, utan kö eller retry |
| Native | Saknas i v1 |
| Audit | Loggrad i fil, utan databas-tabell och signering |
| Tester | Saknas |

> **Observation i den medföljande kodbasen:** `TaxReport.cshtml.cs` bygger för närvarande UTF-8-text och returnerar den med filändelsen `.pdf`; projektets arkitekturdokumentation beskriver också detta som en textfil med `.pdf`-ändelse. Det finns alltså en skillnad mellan casebeskrivningens mål/v1-beskrivning och den implementation som finns i repot.

### v2-mål

- .NET 8
- ASP.NET Core Web API
- EF Core 8 med ledger-pattern för transaktioner
- React 18 SPA med portal-layout, BankID-flöde och FAQ-gränssnitt
- C/C++-modul för PDF-batchgenerering av skatterapporter
- C/C++-modul för PDF-signering med hash och metadata för audit
- Regelstyrd FAQ-sökning mot en kontrollerad FAQ-databas med korrekt träfflogik
- Strukturerad audit-logg i databasen
- Rate limiting på känsliga endpoints

### Native-moduler i v2

#### PDF-generator

PDF-generatorn ska hantera skatterapporter i prestandakritisk batchmiljö, framför allt vid masskörning kring årets slut.

#### PDF-signering

PDF-signeringen ska använda hash och metadata för audit och spårbarhet. Casebeskrivningen anger att modulen ska kunna:

- beräkna en SHA-256-hash av PDF-innehållet
- signera med en privat nyckel
- bädda in signaturen i PDF:ens metadata

## Datamodell

Föreslagna entiteter:

- `Customer`
- `SavingsAccount`
- `InterestRate`
- `Transaction`
- `TaxReport`
- `FaqEntry` – fråga, svar, kategori och nyckelord
- `Notification` – mottagare, typ, referens-ID, status och skickad tid
- `AuditEntry`

## Definition of Done för v2

- Kärnflödet fungerar och kan demonstreras.
- README beskriver installation, körning, testning, kända brister och avgränsningar.
- Teststatus är dokumenterad och rimlig.
- En beslutslogg visar viktiga tekniska vägval.
- Individuella bidrag går att härleda.
- Specialistfeedforward är bearbetad.
- Det finns en fallback om livedemon inte fungerar.
