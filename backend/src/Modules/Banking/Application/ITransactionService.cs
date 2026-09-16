using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nordiska.BuildingBlocks.Database;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Contracts.Responses;

namespace Nordiska.Modules.Banking.Application;

/// <summary>
/// Service abstraction for querying, executing and scheduling transactions and transfers.
/// </summary>
public interface ITransactionService
{
    /// <summary>
    /// Queries transactions, optionally filtered by account id.
    /// </summary>
    Task<IEnumerable<TransactionResponse>> QueryAsync(long? accountId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries paginated transactions using filter and search parameters.
    /// </summary>
    Task<PagedResult<TransactionResponse>> QueryPagedAsync(TransactionQueryParameters parameters, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a transaction by id.
    /// </summary>
    Task<TransactionResponse?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a single transaction (deposit or withdrawal).
    /// </summary>
    Task<TransactionResponse> ExecuteAsync(TransactionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an atomic funds transfer between two accounts.
    /// </summary>
    Task<TransactionResponse> TransferAsync(TransferRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a scheduled/planned transaction.
    /// </summary>
    Task<TransactionResponse> CreatePlannedAsync(PlannedTransactionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels or removes a planned transaction.
    /// </summary>
    Task<bool> CancelPlannedAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes the current balance of a savings account by summing all verified non-planned ledger entries.
    /// </summary>
    Task<decimal> GetBalanceAsync(long accountId, CancellationToken cancellationToken = default);
}
