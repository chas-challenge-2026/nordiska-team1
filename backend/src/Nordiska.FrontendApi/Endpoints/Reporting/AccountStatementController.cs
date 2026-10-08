using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nordiska.FrontendApi.Authentication.Claims;
using Nordiska.Modules.Reporting.Application;
using Nordiska.Modules.Reporting.Contracts.Requests;
using Nordiska.Modules.Reporting.Contracts.Responses;

namespace Nordiska.FrontendApi.Endpoints.Reporting;

[ApiController]
[Route("api/reports/account-statements")]
[Authorize]
public sealed class AccountStatementController : ControllerBase
{
    private readonly IAccountStatementService _service;
    private readonly IAccountStatementQueryService _queries;

    public AccountStatementController(
        IAccountStatementService service,
        IAccountStatementQueryService queries)
    {
        _service = service;
        _queries = queries;
    }

    [HttpPost]
    [ProducesResponseType(
        typeof(AccountStatementAcceptedResponse),
        StatusCodes.Status202Accepted)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountStatementAcceptedResponse>> Create(
        [FromBody] AccountStatementRequest request,
        CancellationToken cancellationToken)
    {
        long customerId = User.GetRequiredCustomerId();

        try
        {
            RequestAccountStatementResult result =
                await _service.RequestAsync(
                    customerId,
                    request.AccountId,
                    request.FromDate,
                    request.ToDate,
                    cancellationToken);

            var response = new AccountStatementAcceptedResponse(
                result.AccountStatementId,
                result.JobId,
                result.Status,
                result.CreatedAt);

            return AcceptedAtAction(
                nameof(GetStatus),
                new { jobId = result.JobId },
                response);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Ogiltig period",
                Detail = exception.Message,
                Status = StatusCodes.Status400BadRequest
            });
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
        typeof(AccountStatementJobResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountStatementJobResponse>> GetStatus(
        long jobId,
        CancellationToken cancellationToken)
    {
        long customerId = User.GetRequiredCustomerId();

        AccountStatementJobStatus? result =
            await _queries.GetStatusAsync(
                customerId,
                jobId,
                cancellationToken);

        if (result is null)
        {
            return NotFound();
        }

        return Ok(new AccountStatementJobResponse(
            result.JobId,
            result.AccountStatementId,
            result.Status,
            result.CreatedAt,
            result.CompletedAt,
            result.Error));
    }

    [HttpGet("{accountStatementId:long}/pdf")]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(
        long accountStatementId,
        CancellationToken cancellationToken)
    {
        long customerId = User.GetRequiredCustomerId();

        AccountStatementDownload? document =
            await _queries.DownloadAsync(
                customerId,
                accountStatementId,
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
