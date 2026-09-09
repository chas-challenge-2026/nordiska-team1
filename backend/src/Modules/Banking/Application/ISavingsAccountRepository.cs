using Nordiska.Modules.Banking.Domain;

namespace Nordiska.Modules.Banking.Application;

public interface ISavingsAccountRepository
{
    Task<IEnumerable<SavingsAccount>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<SavingsAccount?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<long> CreateAsync(SavingsAccount entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(SavingsAccount entity, CancellationToken cancellationToken = default);
}
