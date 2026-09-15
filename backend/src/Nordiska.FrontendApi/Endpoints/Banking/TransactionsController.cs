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
/// API endpoints for querying and executing account transactions (deposits and withdrawals) using the ledger pattern.
/// </summary>
[ApiController]
[Route("api/transactions")]
[Authorize]
public class TransactionsController : ControllerBase
{
    private readonly ITransactionService _service;
    private readonly ISavingsAccountService _savingsAccountService;

    public TransactionsController(ITransactionService service, ISavingsAccountService savingsAccountService)
    {
        _service = service;
        _savingsAccountService = savingsAccountService;
    }

    /// <summary>
    /// Queries transaction ledger entries, optionally filtered by account ID.
    /// </summary>
    /// <param name="accountId">Optional account ID to filter transactions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">List of transactions matching the criteria.</response>
    /// <response code="401">Unauthorized if authentication token is missing or invalid.</response>
    /// <response code="403">Forbidden if accessing transactions for an account that does not belong to the user.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TransactionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<TransactionResponse>>> GetAll([FromQuery] long? accountId, CancellationToken cancellationToken)
    {
        if (accountId.HasValue)
        {
            if (!await IsAuthorizedForAccountAsync(accountId.Value, cancellationToken))
            {
                return Forbid();
            }

            var results = await _service.QueryAsync(accountId, cancellationToken);
            return Ok(results);
        }

        if (IsAdmin())
        {
            var allResults = await _service.QueryAsync(null, cancellationToken);
            return Ok(allResults);
        }

        var currentUserId = GetCurrentUserId();
        var userAccounts = (await _savingsAccountService.GetAllAsync(cancellationToken))
            .Where(a => a.CustomerId == currentUserId)
            .Select(a => a.Id)
            .ToHashSet();

        var allTx = await _service.QueryAsync(null, cancellationToken);
        var filteredTx = allTx.Where(t => userAccounts.Contains(t.AccountId));
        return Ok(filteredTx);
    }

    /// <summary>
    /// Retrieves a specific transaction ledger entry by its identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the transaction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The transaction ledger details.</response>
    /// <response code="401">Unauthorized if authentication token is missing or invalid.</response>
    /// <response code="403">Forbidden if the transaction belongs to another customer's account.</response>
    /// <response code="404">Transaction with the specified ID was not found.</response>
    [HttpGet("{id}", Name = "GetTransactionById")]
    [ProducesResponseType(typeof(TransactionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TransactionResponse>> GetById(long id, CancellationToken cancellationToken)
    {
        var tx = await _service.GetByIdAsync(id, cancellationToken);
        if (tx is null)
        {
            return NotFound(new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "Transaction not found." });
        }

        if (!await IsAuthorizedForAccountAsync(tx.AccountId, cancellationToken))
        {
            return Forbid();
        }

        return Ok(tx);
    }

    /// <summary>
    /// Executes a new transaction (Deposit or Withdrawal) against a savings account.
    /// </summary>
    /// <remarks>
    /// Creates an immutable ledger entry and applies concurrency controls to ensure balance integrity.
    /// Type must be either <c>Deposit</c> or <c>Withdrawal</c>.
    /// </remarks>
    /// <param name="request">Transaction request payload containing accountId, amount, and type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="201">Transaction successfully executed and recorded in the ledger.</response>
    /// <response code="400">Invalid transaction data or insufficient funds for withdrawal.</response>
    /// <response code="401">Unauthorized if authentication token is missing or invalid.</response>
    /// <response code="403">Forbidden if attempting to execute transaction on an account belonging to another customer.</response>
    [HttpPost]
    [ProducesResponseType(typeof(TransactionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TransactionResponse>> Create([FromBody] TransactionRequest request, CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedForAccountAsync(request.AccountId, cancellationToken))
        {
            return Forbid();
        }

        var created = await _service.ExecuteAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Calculates the verified current balance for an account computed from all immutable ledger entries.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The calculated current balance amount.</response>
    /// <response code="401">Unauthorized if authentication token is missing or invalid.</response>
    /// <response code="403">Forbidden if checking balance for another customer's account.</response>
    /// <response code="404">Account not found.</response>
    [HttpGet("balance/{accountId}")]
    [ProducesResponseType(typeof(decimal), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<decimal>> GetBalance(long accountId, CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedForAccountAsync(accountId, cancellationToken))
        {
            return Forbid();
        }

        var balance = await _service.GetBalanceAsync(accountId, cancellationToken);
        return Ok(balance);
    }

    private bool IsAdmin() => User.IsInRole("Admin");

    private long? GetCurrentUserId()
    {
        var currentUserIdStr = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                               ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return long.TryParse(currentUserIdStr, out var currentUserId) ? currentUserId : null;
    }

    private async Task<bool> IsAuthorizedForAccountAsync(long accountId, CancellationToken cancellationToken)
    {
        if (IsAdmin())
        {
            return true;
        }

        var account = await _savingsAccountService.GetByIdAsync(accountId, cancellationToken);
        if (account is null)
        {
            return false;
        }

        return account.CustomerId == GetCurrentUserId();
    }
}
