using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nordiska.FrontendApi.Authentication.Claims;
using Nordiska.Modules.Reporting.Application;
using Nordiska.Modules.Reporting.Contracts.Requests;
using Nordiska.Modules.Reporting.Contracts.Responses;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nordiska.FrontendApi.Endpoints.Reporting;

[ApiController]
[Route("api/reports/tax")]
[Authorize]
public sealed class TaxReportController : ControllerBase
{
    private readonly IAnnualTaxReportService _service;
    private readonly IAnnualTaxReportQueryService _queries;

    public TaxReportController(
        IAnnualTaxReportService service,
        IAnnualTaxReportQueryService queries)
    {
        _service = service;
        _queries = queries;
    }


    [HttpPost]
    [ProducesResponseType(typeof(AnnualTaxReportAcceptedResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]

    public async Task<ActionResult<AnnualTaxReportAcceptedResponse>> Create(TaxReportRequest request,
        CancellationToken ct)
    {
        long customerId = User.GetRequiredCustomerId();

        try
        {
            RequestAnnualTaxReportResult result =
                await _service.RequestAsync(
                    customerId,
                    request.AccountId,
                    request.TaxYear,
                    ct);

            var response =
                new AnnualTaxReportAcceptedResponse(
                    result.TaxReportId,
                    result.JobId,
                    result.Status,
                    result.CreatedAt);

            return Accepted(response);
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Kontot kunde inte hittas",
                Detail = exception.Message,
                Status = StatusCodes.Status404NotFound
            });
        }

    }

    [HttpGet("jobs/{jobId:long}")]
    [ProducesResponseType(
        typeof(AnnualTaxReportJobStatus),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AnnualTaxReportJobStatus>> GetStatus(
        long jobId,
        CancellationToken cancellationToken)
    {
        long customerId = User.GetRequiredCustomerId();

        AnnualTaxReportJobStatus? result =
            await _queries.GetStatusAsync(
                customerId,
                jobId,
                cancellationToken);

        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpGet("{taxReportId:long}/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(
        long taxReportId,
        CancellationToken cancellationToken)
    {
        long customerId = User.GetRequiredCustomerId();

        AnnualTaxReportDownload? document =
            await _queries.DownloadAsync(
                customerId,
                taxReportId,
                cancellationToken);

        if (document is null)
        {
            return NotFound();
        }

        return File(
            document.Content,
            document.ContentType,
            document.FileName);
    }

}


/*
[ApiController]
[Route("api/reports/transactions")]
[Authorize]
public sealed class TaxReportTransactionsController : ControllerBase
{

}
*/
/* info som behövs från db till annual tax report: 
 * Customer:
 * - customerId
 * - customerName
 *
 * Account:
 * - accountNumber
 * - accountName
 *
 * Tax:
 * - taxYear
 * - totalInterestEarned
 * - preliminaryTaxDeducted
 */

public sealed record CreateAnnualTaxReport
    (
        long CustomerId,
        long AccountId,
        int TaxYear
    );

// Redan färdig report
public sealed record GetAnnualTaxReport(
    long TaxReportId
);

public sealed record AnnualTaxReportFullResult(
    long Id,
    long CustomerId,
    long AccountId,
    int TaxYear,
    long TotalInterestMinor,
    long TaxDeductedMinor,
    string Currency,
    string CustomerName,
    string AccountNumber,
    string AccountName,
    DateTimeOffset CreatedAt,
    string SchemaVersion,
    string PayloadHash
);

// På workerns sida:

public sealed record CreateAnnualTaxPdfJob
    (
        long taxReportId
    );

public sealed record AnnualTaxReportSnapshot(
    long Id,
    long CustomerId,
    long AccountId,
    int TaxYear,
    long TotalInterestMinor,
    long TaxDeductedMinor,
    string Currency,
    string CustomerName,
    string AccountNumber,
    string AccountName,
    DateTimeOffset CreatedAt,
    string SchemaVersion,
    string PayloadHash
);

public sealed record CreateDocument(
    string DocumentType,
    byte[] Content,
    string ContentType
);

public sealed record CreateTaxReportDocumentRelation(
    long TaxReportId,
    long DocumentId,
    DateTimeOffset CreatedAt
);

public class TaxReportService
{

}






/*
 *
 *
 * NY IDE:
 *
 * Till databas:
 *
 * Tax Report
 * id, c.id, ac.id, tax_year, total_interest_min, tax_deducted_min, currency,
 * c.name, ac.num, ac.name, created, schema_version, hash
 *
 * Tax Report Job
 * id,
 * tax_report_id (FK)
 * status,
 * created,
  *
 * Tax report documents
 * tax_report_id, // READ till worker
 * document_id, // INSERT till worker
 * created
 *
 *
 *  Så nya flödet blir såhär istället:
 *
 *  1. Api modulen får ett jobb (tax report)
 *  2. Service skapar en tax report rad  (om den ej redan finns)
 *  3. SQL beräknar matematiken (snabbare)
 *  4. Vid skapad tax report får service en bekräftelse såklart
 *  5. Därefter skapas ett Tax Report Job i databasen
 *  6. Worker får en notifikation och får nu en tax report id den har readonly på.
 *  7. Worker hämtar report datan och genererar rapporten i pdf.
 *  8. Vid godkänd generering så skapar den ett document med pdf.
 *  9. Sedan uppdaterar den "document_id" i Tax Report Documents relationstabellen
 *  
 *
 */






/*
 *
 * * 1. API får request om Annual Tax Report.
 *
 * 2. Reporting Service kontrollerar om TaxReport
 *    redan finns för aktuellt konto/år/version.
 *
 * 3. Om inte:
 *    SQL aggregerar nödvändig finansiell data.
 *
 * 4. Reporting Service skapar en immutable
 *    TaxReport snapshot.
 *
 * 5. PayloadHash beräknas och sparas på TaxReport.
 *
 * 6. TaxReportJob skapas med tax_report_id.
 *
 * 7. Worker hittar/får jobbet.
 *
 * 8. Worker har READ ONLY på TaxReport och hämtar
 *    den färdiga rapportdatan.
 *
 * 9. Worker verifierar payload_hash.
 *
 * 10. Worker genererar PDF.
 *
 * 11. Vid lyckad generering skapar Worker Document.
 *
 * 12. Worker INSERT:ar relationen:
 *     (tax_report_id, document_id)
 *
 * 13. Worker uppdaterar Job.Status till Completed.
 *
 *Reporting:
   READ   → nödvändiga Banking-data
   INSERT → TaxReport
   INSERT → TaxReportJob
   
   Worker:
   SELECT → TaxReport
   SELECT → TaxReportJob
   UPDATE → endast Job.Status
   INSERT → Document
   INSERT → TaxReportDocuments
   
   Worker:
   NO ACCESS → Banking
   NO UPDATE → TaxReport
 *
 */







public sealed record AccountStatementDocument(
    [property: JsonPropertyName("title")]
    string Title,

    [property: JsonPropertyName("account_number")]
    string AccountNumber,

    [property: JsonPropertyName("account_name")]
    string AccountName,

    [property: JsonPropertyName("currency")]
    string Currency,

    [property: JsonPropertyName("period")]
    string Period,

    [property: JsonPropertyName("opening_balance")]
    string OpeningBalance,

    [property: JsonPropertyName("closing_balance")]
    string ClosingBalance,

    [property: JsonPropertyName("transactions")]
    IReadOnlyList<AccountTransaction> Transactions
);

public sealed record AccountTransaction(
    [property: JsonPropertyName("date")]
    DateOnly Date,

    [property: JsonPropertyName("type")]
    string Type,

    [property: JsonPropertyName("description")]
    string Description,

    [property: JsonPropertyName("currency")]
    string Currency,

    [property: JsonPropertyName("amount_minor")]
    long AmountMinor,

    [property: JsonPropertyName("amount_display")]
    string AmountDisplay,

    [property: JsonPropertyName("balance_after_display")]
    string BalanceAfterDisplay
);

public sealed record AnnualTaxReportDocument(
    [property: JsonPropertyName("title")]
    string Title,

    [property: JsonPropertyName("tax_year")]
    string TaxYear,

    [property: JsonPropertyName("account_number")]
    string AccountNumber,

    [property: JsonPropertyName("account_name")]
    string AccountName,

    [property: JsonPropertyName("total_interest_earned")]
    string TotalInterestEarned,

    [property: JsonPropertyName("preliminary_tax_deducted")]
    string PreliminaryTaxDeducted,

    [property: JsonPropertyName("reported_to_authority")]
    string ReportedToAuthority
);
public sealed record ReportPackage(
    [property: JsonPropertyName("$schema_version")]
    string SchemaVersion,

    [property: JsonPropertyName("customer_id")]
    long CustomerId,

    [property: JsonPropertyName("customer_name")]
    string CustomerName,

    [property: JsonPropertyName("created_at")]
    DateTimeOffset CreatedAt,

    [property: JsonPropertyName("documents")]
    IReadOnlyList<DocumentEnvelope> Documents
);

public sealed record DocumentEnvelope(
    [property: JsonPropertyName("document_id")]
    string DocumentId,

    [property: JsonPropertyName("kind")]
    string Kind,

    [property: JsonPropertyName("version")]
    string Version,

    [property: JsonPropertyName("document")]
    JsonElement Document
);


