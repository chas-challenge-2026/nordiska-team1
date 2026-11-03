# Frontend Readme och beslutslogg

## Implementering och bearbetning av specialist och kund feed forward

### Expert CTO
- Session och JWT
    - Tillämpades genom att implementera HTTP-only cookie token
    - Token går ut efter 15 minuter
    - Användare loggas automatisk ut efter inaktivitet i 5 minuter

### Expert UX
- Enhetlig och mer central design
    - Utökade funktion av bankkonto sidan
    - Gjorde översikten mer tydlig
    - förbättrade flödet genom sidan för funktioner som används ofta
    - Göm undan kontakt till kundtjänst och gör FAQ mer användbar

### Kund
- Be folk testa sidan
    - Flertalet personer utanför team:et har testat sidan
- Fokus på ränta
    - Mer info om ränta på översikten och tydligare på bankkontosidan
- Rätt grafisk profil
    - Vi har gjort vårat bästa för att följa de färger som de har på nordiska i nuläget.

## Teknik och arkitektur

| Beslut | Motivering | Ansvarig |
| :--- | :---: | ---: |
| React 18 | Kundens önskemål | Teamet |
| Typescript | Bättre typsäkerhet | Teamet |
| Vite | Snabb utvecklingsserver och effektiv byggprocess | Teamet |
| Axios används istället för FetchApi | Förenklar error hantering | Teamet |
| React query för server-state hantering | förenklar flödet av data från backend | Teamet |
| Zustand för UI states | förhindrar att behöva skriva react context för allt | Teamet |
| En singulär Axios instance | En grund instans av axios gör det enklare att använda | Olle |
| Använd react-markdown | för att rendera nordiskas markdownformaterade FAQs | Olle |
| Timeout för hämtning av rapporter | För att förhindra att en användare sitter fast i att hämta en rapport så bör den få en timeout efter 60 sekunder | Olle |


## Funktionalitet och användaruplevelse

| Beslut | Motivering | Ansvarig |
| :--- | :---: | ---: |
| i18n språkbibliotek | Gör applikationen tillgänglig på flera språk, lätt att utöka | Ingrid |
| Olika översiktsvyer | För att matcha användares behov av mer eller mindre utökad information | Ingrid |
| Favoritmarkera konton | Gör det enklare att snabbt komma åt de viktigaste kontona | Ingrid |
| Planerade och återkommande överföringar | Fasta utgifter eller överföringar kan automatiseras | Dennis |
| FAQ delas in i kategorier och fritext sökning | Gör det enklare för användaren att hitta svar | Teamet |
| Relaterade faq:s på sidovyn | För de användare som söker guidning på sidan eller snabbt vill ha ett svar på en fråga | Teamet |
| Olika meddelanden vid utloggning | För att det ska bli tydligt för användaren om utloggning har skett manuellt eller automatiskt | Teamet |
| Interaktiva element håller samma design | För att användaren enkelt ska kunna identifiera knappar och klickbara element | Teamet |
| Driftmeddelanden | Information om driftstörning och meddelanden som behöver nå alla användare oavsett  inloggad eller ej | Ingrid |
| Votering av FAQ | Användaren kan lämna feeback på om en FAQ varit mer eller mindre hjälpsam, presenteras som statistik i Adminportalen | Ingrid |
| Användaren kan själv bestämma layout på översikt och ordning på sektioner | Öka flexibiliteten för användare att själv bestämma vilken information som är av vikt | Olle, Ingrid |


## Säkerhet och inloggning

| Beslut | Motivering | Ansvarig |
| :--- | :---: | ---: |
| BankID används som inloggningsmetod | Kundens önskemål och säker identifiering | Dennis |
| Sessionen kontrolleras och förnyas var 15e minut om användaren är aktiv | Ökar säkerheten och skyddar användarens kontoinformation | Teamet |
| Användaren loggas ut automatiskt efter inaktivitet | Ökar säkerhet och skyddar användares kontoinformation | Ingrid |
| BankID används för externa överföringar | Pengar som lämnar banken kan ej tas tillbaka | Dennis |
| inget konto är förvalt som avsändare | ett förvalt konto höjer risken att pengar skickas från fel konto | Dennis |
| React-router | för att ha väl fungerande routing för olika sorters användarroller och protected routing | Ingrid |


## Design och responsivitet

| Beslut | Motivering | Ansvarig |
| :--- | :---: | ---: |
| Färgpalett, font och bilder hämtas från Nordiskas hemsida | Designval som matchar deras grafiska profil | Teamet |
| Responsiv applikation | Portalen ska kunna användas via dator, surfplatta och mobil | Teamet |
| Delvis olika layouter för desktop och mobilvy | Anpassa navigering och informationspresentation efter enheten | Teamet |
| Återkommande UI element har gemensamma designmönster | Skapar en enhetlig användarupplevelse | Ingrid |
| Tomma tillstånd får egna informationsmeddelanden | Hjälper användaren att förstå varför information saknas och vad nästa steg är | Ingrid |
| Komponentbaserad app | enklare att separera ansvar per komponent och bygg komponenter för att återanvändas | Teamet |
| Separera alla delar av sidan till pages | Enklare att hålla enhetlig design som har samma grund | Teamet |
| WCAG AA| En bank måste fungera för alla, även personer med nedsatt syn eller funktion.| Teamet |


## Data och visualisering

| Beslut | Motivering | Ansvarig |
| :--- | :---: | ---: |
| Transaktioner grupperas per datum (backend) och överföringar slås samman | Ett uttag och en insättning för samma överföring förvirrade användaren, och stängda konton gör listan rörig. | Dennis |
| Sparmålets utveckling visualiseras med en procentbaserad cirkeldiagramvy | För en snabb överblick över hur nära användaren är sitt mål | Ingrid |
| Upplupen ränta redovisas separat från det totala sparandet | Kundens önskemål | Ingrid |
| Saldon och sparande presenteras i tydliga siffror med färgkodning | För tydlighet mot användare och önskemål från kunden att ej visulisera mha grafer | Teamet |


## Adminportal 

| Beslut | Motivering | Ansvarig |
| :--- | :---: | ---: |
| Under håll av FAQ | Enkelt för kundtjänstmedarbetare att skapa, radera och uppdatera FAQ | Ingrid |
| Hantera kundärenden | Ärenden som kunder skickar in från kundportalen ska enkelt hanteras via admin | Olle |
| Utskick av info till kunder | Admin ska kunna skicka ut viktig information till alla kunder | Olle |
| Uppdatering av driftmeddelanden | För admin att uppdatera och pusha ut driftmeddelanden | Ingrid |
| Statistik för FAQ | Visa en visuellt överblick av vad användare söker efter så FAQ kan hållas uppdaterad | Olle |
