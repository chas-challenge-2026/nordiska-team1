using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nordiska.BuildingBlocks.Database;
using Nordiska.FrontendApi.Authentication.Claims;
using Nordiska.FrontendApi.Contracts.Requests;
using Nordiska.FrontendApi.RateLimiting;
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
    /// Queries paginated transaction ledger entries with flexible search, filtering, and sorting parameters.
    /// </summary>
    /// <param name="query">Pagination, filter, and sort criteria.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Paginated list of transactions with metadata.</response>
    /// <response code="400">Invalid query parameters.</response>
    /// <response code="401">Unauthorized if authentication token is missing or invalid.</response>
    /// <response code="404">Account filter contains an account that was not found or does not belong to the user.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<TransactionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(IEnumerable<TransactionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAll([FromQuery] TransactionQueryRequest query, CancellationToken cancellationToken)
    {
        query ??= new TransactionQueryRequest();
        var requestedAccountIds = query.GetRequestedAccountIds();

        if (User.IsAdmin())
        {
            var adminParams = query.ToDomainParameters(requestedAccountIds);
            var adminResult = await _service.QueryPagedAsync(adminParams, cancellationToken);
            return FormatResult(adminResult);
        }

        var userAccounts = (await _savingsAccountService.GetByCustomerIdAsync(User.GetRequiredCustomerId(), cancellationToken))
            .Select(a => a.Id)
            .ToHashSet();

        if (requestedAccountIds != null && requestedAccountIds.Count > 0)
        {
            if (requestedAccountIds.Any(id => !userAccounts.Contains(id)))
            {
                return AccountNotFound();
            }

            var userParams = query.ToDomainParameters(requestedAccountIds);
            var result = await _service.QueryPagedAsync(userParams, cancellationToken);
            return FormatResult(result);
        }

        if (userAccounts.Count == 0)
        {
            var emptyResult = PagedResult<TransactionResponse>.Create(
                Array.Empty<TransactionResponse>(),
                0,
                query.Page,
                query.PageSize);
            return FormatResult(emptyResult);
        }

        var allAccountsParams = query.ToDomainParameters(userAccounts.ToList());
        var pagedResult = await _service.QueryPagedAsync(allAccountsParams, cancellationToken);
        return FormatResult(pagedResult);
    }

    private IActionResult FormatResult(PagedResult<TransactionResponse> result)
    {
        if (HasExplicitPagination())
        {
            return Ok(result);
        }

        return Ok(result.Items);
    }

    private bool HasExplicitPagination()
    {
        return Request.Query.ContainsKey("page") ||
               Request.Query.ContainsKey("pageSize") ||
               Request.Query.ContainsKey("limit") ||
               Request.Query.ContainsKey("offset") ||
               Request.Query.ContainsKey("pageNumber") ||
               Request.Query.ContainsKey("hasMore");
    }

    /// <summary>
    /// Retrieves a specific transaction ledger entry by its identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the transaction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The transaction ledger details.</response>
    /// <response code="401">Unauthorized if authentication token is missing or invalid.</response>
    /// <response code="404">Transaction with the specified ID was not found or belongs to another customer.</response>
    [HttpGet("{id}", Name = "GetTransactionById")]
    [ProducesResponseType(typeof(TransactionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TransactionResponse>> GetById(long id, CancellationToken cancellationToken)
    {
        var tx = await _service.GetByIdAsync(id, cancellationToken);
        if (tx is null || !await IsAuthorizedForAccountAsync(tx.AccountId, cancellationToken))
        {
            return NotFound(new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "Transaction not found." });
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
    /// <response code="404">Account not found or does not belong to the authenticated user.</response>
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Transactions)]
    [ProducesResponseType(typeof(TransactionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TransactionResponse>> Create([FromBody] TransactionRequest request, CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedForAccountAsync(request.AccountId, cancellationToken))
        {
            return AccountNotFound();
        }

        var created = await _service.ExecuteAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Executes a funds transfer between two accounts.
    /// </summary>
    /// <param name="request">Transfer request containing sourceAccountId, targetAccountId, amount, and optional label.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Transfer successfully executed.</response>
    /// <response code="400">Invalid transfer parameters or insufficient funds.</response>
    /// <response code="401">Unauthorized if authentication token is missing or invalid.</response>
    /// <response code="404">Account not found or does not belong to the authenticated user.</response>
    [HttpPost("transfer")]
    [EnableRateLimiting(RateLimitPolicies.Transactions)]
    [ProducesResponseType(typeof(TransactionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TransactionResponse>> Transfer([FromBody] TransferRequest request, CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedForAccountAsync(request.SourceAccountId, cancellationToken))
        {
            return AccountNotFound();
        }

        var result = await _service.TransferAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Creates a scheduled or planned future transaction.
    /// </summary>
    /// <param name="request">Planned transaction details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="201">Planned transaction successfully registered.</response>
    /// <response code="400">Invalid planned transaction details.</response>
    /// <response code="401">Unauthorized if authentication token is missing or invalid.</response>
    /// <response code="404">Account not found or does not belong to the authenticated user.</response>
    [HttpPost("planned")]
    [EnableRateLimiting(RateLimitPolicies.Transactions)]
    [ProducesResponseType(typeof(TransactionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TransactionResponse>> CreatePlanned([FromBody] PlannedTransactionRequest request, CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedForAccountAsync(request.AccountId, cancellationToken))
        {
            return AccountNotFound();
        }

        var created = await _service.CreatePlannedAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Cancels or deletes a planned transaction.
    /// </summary>
    /// <param name="id">Identifier of the planned transaction to cancel.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="204">Planned transaction cancelled successfully.</response>
    /// <response code="401">Unauthorized if authentication token is missing or invalid.</response>
    /// <response code="404">Planned transaction not found or belongs to another customer.</response>
    [HttpDelete("planned/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelPlanned(long id, CancellationToken cancellationToken)
    {
        var existing = await _service.GetByIdAsync(id, cancellationToken);
        if (existing == null || !await IsAuthorizedForAccountAsync(existing.AccountId, cancellationToken))
        {
            return NotFound(new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "Planned transaction not found." });
        }

        var deleted = await _service.CancelPlannedAsync(id, cancellationToken);
        if (!deleted)
        {
            return NotFound(new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "Planned transaction not found." });
        }

        return NoContent();
    }

    /// <summary>
    /// Calculates the verified current balance for an account computed from all immutable ledger entries.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The calculated current balance amount.</response>
    /// <response code="401">Unauthorized if authentication token is missing or invalid.</response>
    /// <response code="404">Account not found or does not belong to the authenticated user.</response>
    [HttpGet("balance/{accountId}")]
    [ProducesResponseType(typeof(decimal), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<decimal>> GetBalance(long accountId, CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedForAccountAsync(accountId, cancellationToken))
        {
            return AccountNotFound();
        }

        var balance = await _service.GetBalanceAsync(accountId, cancellationToken);
        return Ok(balance);
    }

    private async Task<bool> IsAuthorizedForAccountAsync(long accountId, CancellationToken cancellationToken)
    {
        if (User.IsAdmin())
        {
            return true;
        }

        var account = await _savingsAccountService.GetByIdAsync(accountId, cancellationToken);

        return account != null && User.CanAccessCustomer(account.CustomerId);
    }

    // Same response as a missing account, so other customers' account ids can't be discovered
    private NotFoundObjectResult AccountNotFound()
    {
        return NotFound(new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "Account not found." });
    }
}
