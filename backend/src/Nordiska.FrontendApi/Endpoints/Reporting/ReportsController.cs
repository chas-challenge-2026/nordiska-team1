using System;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nordiska.FrontendApi.Authentication.Claims;
using Nordiska.Modules.Reporting.Application;
using Nordiska.Modules.Reporting.Contracts.Requests;

namespace Nordiska.FrontendApi.Endpoints.Reporting;

/// <summary>
/// API endpoints for generating and downloading customer tax statements and annual interest reports.
/// Supports both asynchronous job-based generation and direct synchronous PDF downloads.
/// </summary>
[ApiController]
[Route("api/reports")]
[Authorize]
public sealed class ReportsController : ControllerBase
{
    private readonly ITaxReportService _reportService;

    public ReportsController(ITaxReportService reportService)
    {
        _reportService = reportService;
    }

    /// <summary>
    /// Initiates an asynchronous batch job to generate a tax report (årsbesked) for a savings account.
    /// </summary>
    /// <param name="request">Payload containing accountId and tax year.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="202">Report generation job accepted and currently processing.</response>
    /// <response code="400">Invalid account or tax year specified.</response>
    /// <response code="401">Unauthorized if authentication token is missing or invalid.</response>
    /// <response code="404">Account not found or does not belong to the authenticated user.</response>
    [HttpPost("tax-report")]
    [ProducesResponseType(typeof(TaxReportJobResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaxReportJobResponse>> InitiateTaxReport(
        [FromBody] TaxReportRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetRequiredCustomerId();

        try
        {
            var job = await _reportService.CreateJobAsync(currentUserId, request.AccountId, request.Year, cancellationToken);
            return Accepted(job.DownloadUrl ?? $"/api/reports/jobs/{job.JobId}", job);
        }
        catch (AuthenticationException)
        {
            // Not the customer's account, answer as if it doesn't exist
            return NotFound(new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "Account not found." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails { Status = StatusCodes.Status400BadRequest, Title = ex.Message });
        }
    }

    /// <summary>
    /// Retrieves the status and progress of an asynchronous report generation job.
    /// </summary>
    /// <param name="jobId">The unique report job identifier (e.g. 'job_827f31c-c90a-4b2a').</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The current job status and download metadata.</response>
    /// <response code="401">Unauthorized if authentication token is missing or invalid.</response>
    /// <response code="404">Job with the specified ID was not found or belongs to another customer.</response>
    [HttpGet("jobs/{jobId}")]
    [ProducesResponseType(typeof(TaxReportJobResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaxReportJobResponse>> GetJobStatus(
        string jobId,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetRequiredCustomerId();
        var job = await _reportService.GetJobStatusAsync(jobId, currentUserId, User.IsAdmin(), cancellationToken);

        if (job is null)
        {
            return NotFound(new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "Report job not found." });
        }

        return Ok(job);
    }

    /// <summary>
    /// Downloads the generated, cryptographically signed tax report PDF binary file.
    /// </summary>
    /// <param name="jobId">The unique report job identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns the raw binary PDF file (Content-Type: application/pdf).</response>
    /// <response code="401">Unauthorized if authentication token is missing or invalid.</response>
    /// <response code="404">Report or job was not found or belongs to another customer.</response>
    [HttpGet("jobs/{jobId}/download")]
    [HttpGet("{jobId}/download")]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadReport(
        string jobId,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetRequiredCustomerId();
        var result = await _reportService.GetReportFileAsync(jobId, currentUserId, User.IsAdmin(), cancellationToken);

        if (result is null)
        {
            return NotFound(new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "Report file not found or still processing." });
        }

        return File(result.Value.FileBytes, "application/pdf", result.Value.FileName);
    }

    /// <summary>
    /// Directly generates and downloads a tax report PDF synchronously (v1 MVP direct route).
    /// </summary>
    /// <param name="accountId">The savings account identifier.</param>
    /// <param name="year">The tax year (e.g. 2024, 2025, 2026).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns the generated PDF stream.</response>
    /// <response code="400">Invalid account or tax year.</response>
    /// <response code="401">Unauthorized if authentication token is missing or invalid.</response>
    /// <response code="404">Account not found or does not belong to the authenticated user.</response>
    [HttpGet("tax-report")]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDirectTaxReport(
        [FromQuery] long accountId,
        [FromQuery] int? year,
        CancellationToken cancellationToken)
    {
        var effectiveYear = year ?? DateTime.UtcNow.Year;
        var currentUserId = User.GetRequiredCustomerId();

        try
        {
            var result = await _reportService.GenerateDirectReportAsync(currentUserId, accountId, effectiveYear, User.IsAdmin(), cancellationToken);
            return File(result.FileBytes, "application/pdf", result.FileName);
        }
        catch (AuthenticationException)
        {
            // Not the customer's account, answer as if it doesn't exist
            return NotFound(new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "Account not found." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails { Status = StatusCodes.Status400BadRequest, Title = ex.Message });
        }
    }
}
