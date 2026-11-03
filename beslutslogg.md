# Beslutslogg – PDF-signering

## 1. Förstudie: PDF-signering, PKI och PAdES

Började med att sätta oss in i hur digital PDF-signering fungerar i praktiken, hur nycklar och certifikat används samt vilka PAdES-nivåer som finns.

Fokus låg på att förstå vad som krävs för att en PDF-signatur ska kunna verifieras direkt efter signering, men också hur lösningen kan byggas för långsiktig verifierbarhet.

## 2. Separera nyckel- och certifikathanteringen

Nästa steg var att bryta ut nyckel- och certifikathanteringen från PDF-generatorn.

Beslutet togs för att hålla den säkerhetskritiska delen isolerad och för att göra lösningen enklare att utveckla, testa och byta ut senare.

PKCS#11 gör att signeringskoden inte behöver känna till hur den privata nyckeln lagras. Under utveckling kan ett mjukvarubaserat nyckellager användas (SoftHSM) används för utveckling och demo, medan samma arkitektur senare kan kopplas mot ett riktigt HSM utan att PDF-generatorns signeringsflöde behöver ändras.

## 3. Första signeringsflödet

När nyckel- och certifikathanteringen var på plats implementerades det första signeringsflödet med OpenSSL och PKCS#11.

PDF-generatorn räknar fram en SHA-256-digest över dokumentets signerade ByteRange. Endast denna digest skickas vidare till signer-modulen.

Detta var ett medvetet designbeslut: signer-tjänsten behöver inte känna till hela PDF-dokumentet eller hantera kunddata. Det minskar både ansvar och attackyta för den mest säkerhetskritiska delen av modulen.

## 4. Från nyckelladdning per dokument till långlivad signer-tjänst

Den första implementationen laddade nyckel och certifikat för varje dokument som skulle signeras.

Det fungerade, men innebar onödig overhead vid varje signeringsförfrågan.

Lösningen ändrades därför till en långlivad signer-tjänst där OpenSSL-provider, PKCS#11-kontext, privat nyckel och certifikat initieras vid uppstart och återanvänds mellan signeringar.

Det gav bättre prestanda och en tydligare separation mellan PDF-generatorn och den kryptografiska implementationen.

## 5. Unix domain socket och definierat protokoll

För kommunikationen mellan PDF-generatorn och signer-tjänsten valdes en Unix domain socket.

Ett separat binärt protokoll definierades där PDF-generatorn skickar bland annat digest-algoritm, signeringsläge och SHA-256-digest, och får tillbaka den färdiga signaturen.

Det innebär att PDF-generatorn och signer-tjänsten är löst kopplade och kan utvecklas separat.

## 6. CMS/CAdES i stället för rå signatur

Nästa steg var att bygga signaturen som CMS/CAdES i stället för att bara returnera en rå kryptografisk signatur.

CMS/CAdES gör det möjligt att inkludera certifikat och signeringsattribut i en standardiserad signaturcontainer och passar därför bättre för PDF/PAdES-signering.

Detta blev grunden för att senare kunna lägga till tidsstämpling och högre nivåer av långsiktig validering.

## 7. RFC 3161-tidsstämpling

Stöd för tidsstämpling enligt RFC 3161 lades därefter till.

Signaturen kompletteras med ett TimeStampToken från en TSA, vilket gör det möjligt att verifiera att signaturen existerade vid en betrodd tidpunkt.

Detta ger stöd för PAdES B-T och skapar en tydlig väg vidare mot B-LT och B-LTA.

## 8. Lokal TSA för utveckling och demo

För utveckling och demo byggdes en egen RFC 3161-kompatibel TSA-server.

Syftet var att kunna testa hela tidsstämplingsflödet utan att vara beroende av externa TSA-leverantörer eller kostnader per förfrågan.

Servern använder samma standardiserade RFC 3161-protokoll som externa leverantörer, vilket gör att den lokala TSA:n senare kan ersättas med en extern betrodd TSA utan att signeringsflödet behöver designas om.

## 9. Säker hantering av PIN-koder och känsligt minne

När signer-tjänsten blev långlivad blev även hanteringen av credentials viktigare.

PIN-koder och andra känsliga värden hämtas därför först när de behövs, exempelvis via callbacks, i stället för att spridas genom flera lager i systemet.

Där känsligt material måste finnas i minnet används explicit minnesnollställning så att informationen inte ligger kvar längre än nödvändigt.

Runtime-secrets och miljöspecifik konfiguration hålls också utanför låg-nivåmodulerna. Detta gör att nyckelladdningen förblir generell och att samma kod kan användas i olika miljöer.

## 10. Parallellisering av signer och TSA

De första versionerna av signer- och TSA-servern hanterade förfrågningar seriellt.

För att kunna hantera större mängder dokument parallellt infördes worker-pooler och begränsade request-köer.

Signer-tjänsten använder flera workers där varje worker har en egen långlivad signer-instans. På så sätt behöver inte känsligt OpenSSL- eller PKCS#11-state delas mellan trådar.

TSA-servern byggdes enligt samma princip.

## 11. Belastningstest och verifiering

När concurrency-stödet var på plats kompletterades lösningen med concurrency- och stresstester.

Syftet var att verifiera att både signer och TSA klarar många parallella förfrågningar utan fel och utan att tappa den separation som finns mellan worker-instanserna.

Testerna visade att lösningen klarar betydligt högre belastning än det förväntade behovet för demo- och banksystemet.

## 12. Nuvarande riktning

Resultatet är en modulär signeringsarkitektur där:

- PDF-generatorn ansvarar för PDF-strukturen och ByteRange.
- signer-tjänsten ansvarar för CMS/CAdES och nyckelanvändning.
- PKCS#11 abstraherar nyckellagret.
- TSA ansvarar för RFC 3161-tidsstämpling.
- kommunikationen sker via ett tydligt protokoll över Unix domain socket.

Det gör att varje del kan utvecklas, skalas eller bytas ut separat.

Nästa naturliga steg för full långsiktig PAdES-validering är att komplettera PDF-lagret med validation data, exempelvis certifikatkedjor och revocation-data, samt document timestamps för B-LT och B-LTA.
