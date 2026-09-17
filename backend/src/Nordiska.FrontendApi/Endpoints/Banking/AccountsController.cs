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
/// API endpoints for managing customer bank accounts and account information.
/// </summary>
[ApiController]
[Route("api/accounts")]
[Route("api/savingsaccounts")]
[Tags("Accounts")]
[Authorize]
public class AccountsController : ControllerBase
{
    private readonly ISavingsAccountService _service;

    public AccountsController(ISavingsAccountService service)
    {
        _service = service;
    }

    /// <summary>
    /// Retrieves bank accounts. Non-admin users only receive their own accounts. Optionally filters by account type.
    /// </summary>
    /// <param name="type">Optional account type filter (e.g., 'saving', 'standard', 'flex', 'fix', 'premium').</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">List of bank accounts.</response>
    /// <response code="401">Unauthorized if authentication token is missing or invalid.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<SavingsAccountResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<SavingsAccountResponse>>> GetAll([FromQuery] string? type, CancellationToken cancellationToken)
    {
        // Filter in the query so other customers' accounts never leave the database
        var results = User.IsAdmin()
            ? await _service.GetAllAsync(cancellationToken)
            : await _service.GetByCustomerIdAsync(User.GetRequiredCustomerId(), cancellationToken);

        if (!string.IsNullOrWhiteSpace(type))
        {
            results = results.Where(a => a.AccountType.Equals(type, StringComparison.OrdinalIgnoreCase));
        }
        return Ok(results);
    }

    /// <summary>
    /// Retrieves details for a specific bank account by its identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the bank account.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns the bank account details.</response>
    /// <response code="401">Unauthorized if authentication token is missing or invalid.</response>
    /// <response code="404">Bank account was not found or does not belong to the authenticated customer.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(SavingsAccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SavingsAccountResponse>> GetById(long id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);

        // Same response for "not found" and "not yours", so other customers' account ids can't be discovered
        if (item is null || !User.CanAccessCustomer(item.CustomerId))
        {
            return NotFound(new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "Bank account not found." });
        }

        return Ok(item);
    }

    /// <summary>
    /// Opens a new bank account for the authenticated customer.
    /// </summary>
    /// <remarks>
    /// Creates a bank account with initial interest rate and configuration linked to the customer.
    /// </remarks>
    /// <param name="request">Payload containing customerId, optional accountNumber, accountName, accountType, and interestRate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="201">Bank account successfully created.</response>
    /// <response code="400">Invalid account creation request data.</response>
    /// <response code="401">Unauthorized if authentication token is missing or invalid.</response>
    /// <response code="404">Customer not found when attempting to open an account for another customer.</response>
    [HttpPost]
    [AuditAction("ACCOUNT_OPEN")]
    [ProducesResponseType(typeof(SavingsAccountResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SavingsAccountResponse>> Create([FromBody] OpenSavingsAccountRequest request, CancellationToken cancellationToken)
    {
        if (!User.CanAccessCustomer(request.CustomerId))
        {
            return NotFound(new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "Customer not found." });
        }

        var created = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Closes a bank account if its balance is zero.
    /// </summary>
    /// <param name="id">The unique identifier of the bank account to close.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Account successfully closed.</response>
    /// <response code="400">Cannot close account with positive balance.</response>
    /// <response code="401">Unauthorized if authentication token is missing or invalid.</response>
    /// <response code="404">Account not found or does not belong to the authenticated customer.</response>
    [HttpPatch("{id}/close")]
    [HttpPost("{id}/close")]
    [AuditAction("ACCOUNT_CLOSE")]
    [ProducesResponseType(typeof(SavingsAccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SavingsAccountResponse>> Close(long id, CancellationToken cancellationToken)
    {
        var existing = await _service.GetByIdAsync(id, cancellationToken);
        if (existing is null || !User.CanAccessCustomer(existing.CustomerId))
        {
            return NotFound(new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "Account not found." });
        }

        var closed = await _service.CloseAccountAsync(id, cancellationToken);
        return Ok(closed);
    }
}
