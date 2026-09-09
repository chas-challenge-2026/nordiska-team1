using Nordiska.Modules.Banking.Domain;

namespace Nordiska.Modules.Banking.Application;

public interface ITransactionRepository
{
    Task<IEnumerable<LedgerEntry>> QueryAsync(long? accountId = null, CancellationToken cancellationToken = default);
    Task<LedgerEntry?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<long> CreateAsync(LedgerEntry entry, CancellationToken cancellationToken = default);
}
