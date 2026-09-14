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
public class SavingsAccountsController : ControllerBase
{
    private readonly ISavingsAccountService _service;

    public SavingsAccountsController(ISavingsAccountService service)
    {
        _service = service;
    }

    /// <summary>
    /// Retrieves all savings accounts across the bank.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">List of all active savings accounts.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<SavingsAccountResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SavingsAccountResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var results = await _service.GetAllAsync(cancellationToken);
        return Ok(results);
    }

    /// <summary>
    /// Retrieves details for a specific savings account by its identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the savings account.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns the savings account details.</response>
    /// <response code="404">Savings account with the specified ID was not found.</response>
    [HttpGet("{id}", Name = "GetSavingsAccountById")]
    [ProducesResponseType(typeof(SavingsAccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SavingsAccountResponse>> GetById(long id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return Ok(item);
    }

    /// <summary>
    /// Opens a new savings account for a customer.
    /// </summary>
    /// <remarks>
    /// Creates a savings account with initial interest rate and configuration linked to a customer.
    /// </remarks>
    /// <param name="request">Payload containing customerId, accountNumber, accountType, and interestRate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="201">Savings account successfully created.</response>
    /// <response code="400">Invalid account creation request data.</response>
    [HttpPost]
    [ProducesResponseType(typeof(SavingsAccountResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SavingsAccountResponse>> Create([FromBody] OpenSavingsAccountRequest request, CancellationToken cancellationToken)
    {
        var created = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }
}
