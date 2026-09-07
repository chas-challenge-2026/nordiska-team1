using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Contracts.Responses;

namespace Nordiska.Modules.Banking.Application;

public interface ITransactionService
{
    Task<IEnumerable<TransactionResponse>> QueryAsync(long? accountId = null, CancellationToken cancellationToken = default);
    Task<TransactionResponse> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<TransactionResponse> ExecuteAsync(TransactionRequest request, CancellationToken cancellationToken = default);
}
