using Microsoft.EntityFrameworkCore;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Domain;
using Nordiska.Modules.Banking.Infrastructure.Db;

namespace Nordiska.Modules.Banking.Infrastructure;

public sealed class AccountTypeConfigRepository(BankingDbContext db) : IAccountTypeConfigRepository
{
    public async Task<IEnumerable<AccountTypeConfig>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await db.AccountTypeConfigs
            .AsNoTracking()
            .OrderBy(c => c.AccountType)
            .ToListAsync(cancellationToken);
    }
}
