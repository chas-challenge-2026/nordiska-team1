using Nordiska.Modules.Banking.Domain;

namespace Nordiska.Modules.Banking.Application;

public interface IAccountTypeConfigRepository
{
    Task<IEnumerable<AccountTypeConfig>> GetAllAsync(CancellationToken cancellationToken = default);
}
