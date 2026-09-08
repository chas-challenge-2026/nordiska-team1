using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Contracts.Responses;
namespace Nordiska.FrontendApi.Endpoints.Banking;
/// <summary>
/// API endpoints for querying and executing transactions.
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
    /// Queries transactions, optionally filtered by account id.
    /// </summary>
    /// <param name="accountId">Optional account id to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of transaction responses.</returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TransactionResponse>>> GetAll([FromQuery] long? accountId, CancellationToken cancellationToken)
    {
        var results = await _service.QueryAsync(accountId, cancellationToken);
        return Ok(results);
    }
    /// <summary>
    /// Retrieves a transaction by id.
    /// </summary>
    /// <param name="id">Transaction id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Transaction response.</returns>
    [HttpGet("{id}", Name = "GetTransactionById")]
    public async Task<ActionResult<TransactionResponse>> GetById(long id, CancellationToken cancellationToken)
    {
        var tx = await _service.GetByIdAsync(id, cancellationToken);
        return Ok(tx);
    }
    /// <summary>
    /// Executes a transaction (deposit or withdrawal) on an account.
    /// </summary>
    /// <param name="request">Transaction request payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Created transaction response.</returns>
    [HttpPost]
    // Apply withdrawal sliding-window policy. To get per-account limits, clients must set header X-Account-Id with the account id.
    [EnableRateLimiting("WithdrawalPolicy")]
    public async Task<ActionResult<TransactionResponse>> Create([FromBody] TransactionRequest request, CancellationToken cancellationToken)
    {
        var created = await _service.ExecuteAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Gets the current verified balance for an account computed from all ledger entries.
    /// </summary>
    /// <param name="accountId">Account id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The calculated balance amount.</returns>
    [HttpGet("balance/{accountId}")]
    public async Task<ActionResult<decimal>> GetBalance(long accountId, CancellationToken cancellationToken)
    {
        var balance = await _service.GetBalanceAsync(accountId, cancellationToken);
        return Ok(balance);
    }
}
