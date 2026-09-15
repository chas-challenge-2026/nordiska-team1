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
/// API endpoints for managing customer savings accounts and account information.
/// </summary>
[ApiController]
[Route("api/savingsaccounts")]
[Authorize]
public class SavingsAccountsController : ControllerBase
{
    private readonly ISavingsAccountService _service;

    public SavingsAccountsController(ISavingsAccountService service)
    {
        _service = service;
    }

    /// <summary>
    /// Retrieves savings accounts. Non-admin users only receive their own accounts.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">List of savings accounts.</response>
    /// <response code="401">Unauthorized if authentication token is missing or invalid.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<SavingsAccountResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<SavingsAccountResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var results = await _service.GetAllAsync(cancellationToken);
        if (!IsAdmin())
        {
            var currentUserId = GetCurrentUserId();
            results = results.Where(a => a.CustomerId == currentUserId);
        }
        return Ok(results);
    }

    /// <summary>
    /// Retrieves details for a specific savings account by its identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the savings account.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns the savings account details.</response>
    /// <response code="401">Unauthorized if authentication token is missing or invalid.</response>
    /// <response code="403">Forbidden if the account does not belong to the authenticated customer.</response>
    /// <response code="404">Savings account with the specified ID was not found.</response>
    [HttpGet("{id}", Name = "GetSavingsAccountById")]
    [ProducesResponseType(typeof(SavingsAccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SavingsAccountResponse>> GetById(long id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        if (item is null)
        {
            return NotFound(new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "Savings account not found." });
        }

        if (!IsAdmin() && item.CustomerId != GetCurrentUserId())
        {
            return Forbid();
        }

        return Ok(item);
    }

    /// <summary>
    /// Opens a new savings account for the authenticated customer.
    /// </summary>
    /// <remarks>
    /// Creates a savings account with initial interest rate and configuration linked to the customer.
    /// </remarks>
    /// <param name="request">Payload containing customerId, accountNumber, accountType, and interestRate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="201">Savings account successfully created.</response>
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

    private bool IsAdmin() => User.IsInRole("Admin");

    private long? GetCurrentUserId()
    {
        var currentUserIdStr = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                               ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return long.TryParse(currentUserIdStr, out var currentUserId) ? currentUserId : null;
    }
}
