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
public class TransactionsController : ControllerBase
{
    private readonly ITransactionService _service;

    public TransactionsController(ITransactionService service)
    {
        _service = service;
    }

    /// <summary>
    /// Queries transaction ledger entries, optionally filtered by account ID.
    /// </summary>
    /// <param name="accountId">Optional account ID to filter transactions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">List of transactions matching the criteria.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TransactionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TransactionResponse>>> GetAll([FromQuery] long? accountId, CancellationToken cancellationToken)
    {
        var results = await _service.QueryAsync(accountId, cancellationToken);
        return Ok(results);
    }

    /// <summary>
    /// Retrieves a specific transaction ledger entry by its identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the transaction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The transaction ledger details.</response>
    /// <response code="404">Transaction with the specified ID was not found.</response>
    [HttpGet("{id}", Name = "GetTransactionById")]
    [ProducesResponseType(typeof(TransactionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TransactionResponse>> GetById(long id, CancellationToken cancellationToken)
    {
        var tx = await _service.GetByIdAsync(id, cancellationToken);
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
    [HttpPost]
    [ProducesResponseType(typeof(TransactionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TransactionResponse>> Create([FromBody] TransactionRequest request, CancellationToken cancellationToken)
    {
        var created = await _service.ExecuteAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Calculates the verified current balance for an account computed from all immutable ledger entries.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The calculated current balance amount.</response>
    /// <response code="404">Account not found.</response>
    [HttpGet("balance/{accountId}")]
    [ProducesResponseType(typeof(decimal), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<decimal>> GetBalance(long accountId, CancellationToken cancellationToken)
    {
        var balance = await _service.GetBalanceAsync(accountId, cancellationToken);
        return Ok(balance);
    }
}
