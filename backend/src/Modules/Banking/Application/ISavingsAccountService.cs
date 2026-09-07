using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Contracts.Responses;

namespace Nordiska.Modules.Banking.Application;

public interface ISavingsAccountService
{
    Task<IEnumerable<SavingsAccountResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<SavingsAccountResponse> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<SavingsAccountResponse> CreateAsync(OpenSavingsAccountRequest request, CancellationToken cancellationToken = default);
}
