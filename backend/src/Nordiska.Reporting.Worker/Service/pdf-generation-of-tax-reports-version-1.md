
# MVP for Version 1

The most important issues at this moment is **poor data management/security** and **bad optimization**.
So for this first version, the main focus will be to implement a robust workflow that is scalable to the final version without having to make large structural changes later. 

# What is done
 
# Database
1) Create a table for TaxJob  
2) Create two triggers 

# Frontend

># **TaxReportJob (Service)**
>1)  Create an empty service for TaxReportJobService
>2) Create methods -> 
>***CreateTaxReportJob(CreateTaxReportJob job) out Task<Reponse<TaxReportOverview>>***  
>***CheckJobStatus(CreateTaxReportJob job) out Task<Response<TaxReport>>***
>***GetTaxReportOverview(int accountId, int year) out Task<Response<TaxReportOverview>>***


># **TaxReport (Controller)**
>1)  Create an empty controller for TaxReportController
>2) Create a GET controller that retrieves the status
>3) Create a GET controller that retrieves the tax report
>4) Comment out authentication

# Worker
># **TaxReportJob (Service)**
>1)  Create an empty service for TaxReportJobService
>2) Create methods -> 
>***PingTaxReport(CreateTaxReportJob job) out Task<Reponse<TaxReportOverview>>***  
>***CheckJobStatus(CreateTaxReportJob job) out Task<Response<TaxReport>>***
>***GeneratePdfForTaxes(int accountId, int year) out Task<Response<TaxReportOverview>>***
 

# Initial planning for version 1

  ## In the API

	#### 1)  The FrontendApi Controller validates the identity via JWT (indirectly validating that the user exists here)
  #### 2)  It then instructs the Service to update TaxReportJob in the database
  #### 3) The Service performs two validations (that the user exists and that a tax report does not already exist with a URL)
  #### 4)  The Repository updates the table with a new row

>PostgreSQL has two triggers on the TaxReportJob table: one when a row is created and one when the status field is updated


## In the Worker

	#### 1) The Worker receives a "ping" that a row has been updated through a trigger
  #### 2) It retrieves the row
  #### 3) It validates the data first
  #### 4) It then runs the native generation
  #### 5) The Status field is updated periodically through a stream

  ## API
#### 1) The API (possibly a small worker?) receives a ping that the status has been updated through a trigger. (In v1, this is a separate controller because streaming is somewhat complicated for v1.)

## In the Worker
#### 1) When the job is complete, status is updated to done and the URL is set to the file. In case of an error, an error code field (int) is updated.

  ## API
#### 1) The API (possibly a small worker, such as a notification worker in the API) receives a ping through a trigger that the status has been updated to Done.
-> If the CancellationToken is active (meaning the user is still connected), it sends the PDF back.
<- Otherwise, only the Notification table is updated.













 









