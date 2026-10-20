using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nordiska.FrontendApi.Authentication.Claims;
using Nordiska.FrontendApi.Filters;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Contracts.Responses;

namespace Nordiska.FrontendApi.Endpoints.Banking;

/// <summary>
/// API endpoints for customer loans.
/// </summary>
[ApiController]
[Route("api/loans")]
[Tags("Loans")]
[Authorize]
public sealed class LoansController : ControllerBase
{
    private readonly ILoanService _service;

    public LoansController(ILoanService service)
    {
        _service = service;
    }

    /// <summary>
    /// Retrieves loans. Non-admin users only receive their own loans.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">List of loans.</response>
    /// <response code="401">Unauthorized if authentication token is missing or invalid.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<LoanResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<LoanResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var results = User.IsAdmin()
            ? await _service.GetAllAsync(cancellationToken)
            : await _service.GetByCustomerIdAsync(User.GetRequiredCustomerId(), cancellationToken);

        return Ok(results);
    }

    /// <summary>
    /// Retrieves a loan by its identifier.
    /// </summary>
    /// <remarks>
    /// The outstanding amount includes interest accrued up to today.
    /// </remarks>
    /// <param name="id">The unique identifier of the loan.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns the loan.</response>
    /// <response code="401">Unauthorized if authentication token is missing or invalid.</response>
    /// <response code="404">Loan was not found or does not belong to the authenticated customer.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(LoanResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LoanResponse>> GetById(long id, CancellationToken cancellationToken)
    {
        var loan = await _service.GetByIdAsync(id, cancellationToken);

        // Same response for "not found" and "not yours", so other customers' loan ids can't be discovered
        if (!User.CanAccessCustomer(loan.CustomerId))
        {
            return NotFound(new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "Loan not found." });
        }

        return Ok(loan);
    }

    /// <summary>
    /// Applies for a personal loan for the authenticated customer.
    /// </summary>
    /// <remarks>
    /// The loan is approved right away and paid out to the given account. The interest rate is the Riksbank policy rate plus a margin.
    /// </remarks>
    /// <param name="request">Payload containing amount, termMonths and payoutAccountId.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="201">Loan created and paid out.</response>
    /// <response code="400">Amount or term outside the allowed limits.</response>
    /// <response code="401">Unauthorized if authentication token is missing or invalid.</response>
    /// <response code="404">Payout account not found or does not belong to the authenticated customer.</response>
    /// <response code="409">Payout account is closed or the total personal loan debt would be too high.</response>
    [HttpPost]
    [AuditAction("LOAN_APPLY")]
    [ProducesResponseType(typeof(LoanResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LoanResponse>> Apply([FromBody] ApplyForLoanRequest request, CancellationToken cancellationToken)
    {
        var created = await _service.ApplyAsync(User.GetRequiredCustomerId(), request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Repays part of or the whole loan from one of the customer's accounts.
    /// </summary>
    /// <remarks>
    /// Interest accrued up to today is added to the loan before the repayment is subtracted. The loan is marked repaid when nothing is left.
    /// </remarks>
    /// <param name="id">The unique identifier of the loan.</param>
    /// <param name="request">Payload containing amount and fromAccountId.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Repayment registered, returns the updated loan.</response>
    /// <response code="400">Invalid repayment request data.</response>
    /// <response code="401">Unauthorized if authentication token is missing or invalid.</response>
    /// <response code="404">Loan or account not found or does not belong to the authenticated customer.</response>
    /// <response code="409">Loan is not active, the amount exceeds what is owed or the account has insufficient funds.</response>
    [HttpPost("{id}/repayments")]
    [AuditAction("LOAN_REPAY")]
    [ProducesResponseType(typeof(LoanResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LoanResponse>> Repay(long id, [FromBody] RepayLoanRequest request, CancellationToken cancellationToken)
    {
        var existing = await _service.GetByIdAsync(id, cancellationToken);
        if (!User.CanAccessCustomer(existing.CustomerId))
        {
            return NotFound(new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "Loan not found." });
        }

        var updated = await _service.RepayAsync(id, request, cancellationToken);
        return Ok(updated);
    }
}
