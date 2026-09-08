using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Contracts.Responses;

namespace Nordiska.Modules.Banking.Application;

/// <summary>
/// Service abstraction for querying and executing transactions.
/// </summary>
public interface ITransactionService
{
    /// <summary>
    /// Queries transactions, optionally filtered by account id.
    /// </summary>
    Task<IEnumerable<TransactionResponse>> QueryAsync(long? accountId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a transaction by id.
    /// </summary>
    Task<TransactionResponse> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a transaction (deposit or withdrawal).
    /// </summary>
    Task<TransactionResponse> ExecuteAsync(TransactionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes the current balance of a savings account by summing all verified ledger entries.
    /// </summary>
    Task<decimal> GetBalanceAsync(long accountId, CancellationToken cancellationToken = default);
}
