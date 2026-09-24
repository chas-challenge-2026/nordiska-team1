 ## I Api

  #### 1)  FrontendApi Controller validerar identitet via JWT (indirekt validerar existens på user här)
  #### 2)  Därefter skickar till Service att uppdatera TaxReportJob i databasen
  #### 3) Service utför två valideringar (existens och att en skatterapport inte redan finns och har URL)
  #### 4)  Repository uppdaterar tabellen med en ny rad

>PostgreSql har två triggers på TaxReportJob tabellen: när en rad skapas och när status-fältet uppdateras


## I Worker

  #### 1) Worker får en "ping" att en rad uppdaterats via trigger
  #### 2) Den hämtar raden
  #### 3) Validerar datan först
  #### 4) Sedan kör den Native generering 
  #### 5) Via en stream uppdateras periodvis Status-fältet 

 ##  Api
#### 1) Api (kanske en liten worker?) får en ping att status uppdaterats via Trigger. (I v1 är detta en egen controller då streaming är lite komplicerat för v1)

## I Worker 
#### 1) Vid färdigt jobb uppdateras status = done, url = filen / Vid error uppdateras ett felkodsfält (int)

 ##  Api
#### 1) Api (kanske en liten worker (notification worker i api?) får en ping att status uppdaterats till Done via Trigger 
-> Om CancellationToken är aktiv (alltså user är kvar) så skickar den tillbaka PDF
<- Annars uppdateras bara Notification-table













 




<!-- ARCH_DIAGRAM_STATE: {"modules":[],"connections":[],"methodologyNodes":[],"headlessIcons":[],"pattern":"blank"} -->
