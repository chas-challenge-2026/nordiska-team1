using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
        var results = await _service.GetAllAsync(cancellationToken);
        if (!IsAdmin())
        {
            var currentUserId = GetCurrentUserId();
            results = results.Where(a => a.CustomerId == currentUserId);
        }
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
    /// <response code="403">Forbidden if the account does not belong to the authenticated customer.</response>
    /// <response code="404">Bank account with the specified ID was not found.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(SavingsAccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SavingsAccountResponse>> GetById(long id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        if (item is null)
        {
            return NotFound(new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "Bank account not found." });
        }

        if (!IsAdmin() && item.CustomerId != GetCurrentUserId())
        {
            return Forbid();
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
    /// <response code="403">Forbidden if attempting to open an account for another customer.</response>
    [HttpPost]
    [ProducesResponseType(typeof(SavingsAccountResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SavingsAccountResponse>> Create([FromBody] OpenSavingsAccountRequest request, CancellationToken cancellationToken)
    {
        if (!IsAdmin() && request.CustomerId != GetCurrentUserId())
        {
            return Forbid();
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
    /// <response code="403">Forbidden if the account does not belong to the authenticated customer.</response>
    /// <response code="404">Account not found.</response>
    [HttpPatch("{id}/close")]
    [HttpPost("{id}/close")]
    [ProducesResponseType(typeof(SavingsAccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SavingsAccountResponse>> Close(long id, CancellationToken cancellationToken)
    {
        var existing = await _service.GetByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            return NotFound(new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "Account not found." });
        }

        if (!IsAdmin() && existing.CustomerId != GetCurrentUserId())
        {
            return Forbid();
        }

        var closed = await _service.CloseAccountAsync(id, cancellationToken);
        return Ok(closed);
    }

    private bool IsAdmin() => User.IsInRole("Admin");

    private long? GetCurrentUserId()
    {
        var currentUserIdStr = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                               ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return long.TryParse(currentUserIdStr, out var currentUserId) ? currentUserId : null;
    }
}
