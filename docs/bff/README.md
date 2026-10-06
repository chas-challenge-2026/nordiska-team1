# BFF - före och efter

**BFF betyder: ett API som är gjort för just vår frontend.**

## Före: 16 API-operationer

Frontend måste själv kombinera data och känna till flera delar av backend.

| Område | Antal |
|---|---:|
| Inloggning och BankID | 6 |
| Kund | 1 |
| Konton | 4 |
| Transaktioner och överföringar | 5 |
| **Totalt** | **16** |

## Efter: 13 BFF-operationer

BFF samlar det varje sida behöver och skickar ett enkelt svar till frontend.

| Område | Antal |
|---|---:|
| Session och registrering | 4 |
| Sidornas data | 4 |
| Skapa, ändra och avsluta | 5 |
| **Totalt** | **13** |

## Skillnaden i vardagen

| Flöde | Före | Med BFF |
|---|---:|---:|
| Registrera kund och skapa första kontot | 3 anrop | 1 anrop |
| Öppna sidan Transaktioner | 2 anrop | 1 anrop |
| Öppna sidan Överföring | 2 anrop | 1 anrop |

```text
FÖRE:  Frontend -> flera delar av backend
EFTER: Frontend -> BFF -> resten av backend
```

## Varför det är bättre här

- Mindre kod i frontend.
- Färre anrop när en sida öppnas.
- Kundens identitet och bankens regler bestäms i backend.
- Backend kan byggas om utan att frontend måste byggas om samtidigt.

> Vinsten är inte bara 16 till 13. Den stora vinsten är att varje sida får ett enkelt anrop och att känsliga regler stannar i backend.

## Föreslagna endpoints

| Metod | Endpoint | Vad den gör |
|---|---|---|
| `POST` | `/api/session` | Loggar in eller startar BankID |
| `GET` | `/api/session` | Kontrollerar inloggningen |
| `DELETE` | `/api/session` | Loggar ut |
| `POST` | `/api/onboarding` | Registrerar kund och skapar första kontot |
| `GET` | `/api/dashboard` | Hämtar allt som startsidan behöver |
| `GET` | `/api/accounts` | Hämtar kundens konton |
| `POST` | `/api/accounts` | Öppnar ett nytt konto |
| `POST` | `/api/accounts/{id}/closure` | Begär att kontot ska avslutas |
| `GET` | `/api/transactions` | Hämtar och filtrerar transaktioner |
| `GET` | `/api/transfer` | Hämtar allt som överföringssidan behöver |
| `POST` | `/api/transfers` | Gör eller planerar en överföring |
| `DELETE` | `/api/transfers/{id}` | Avbryter en planerad överföring |
| `PATCH` | `/api/profile` | Ändrar e-post eller telefonnummer |

## Enkla dataflöden

```text
Öppna startsidan
Frontend -> GET /api/dashboard -> BFF -> backend -> ett svar till frontend

Registrera kund
Frontend -> POST /api/onboarding -> BFF -> kund + konto + inloggning -> klart

Göra en överföring
Frontend -> POST /api/transfers -> BFF kontrollerar reglerna -> överföring -> resultat
```
