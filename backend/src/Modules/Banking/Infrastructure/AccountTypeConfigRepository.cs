using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

    public async Task<AccountTypeConfig?> GetByTypeAsync(string accountType, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accountType)) return null;
        var normalized = accountType.Trim().ToLowerInvariant();
        return await db.AccountTypeConfigs.FirstOrDefaultAsync(
            x => x.AccountType.ToLower() == normalized, 
            cancellationToken);
    }

    public async Task CreateAsync(AccountTypeConfig entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        entity.AccountType = entity.AccountType.Trim().ToLowerInvariant();
        db.AccountTypeConfigs.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(AccountTypeConfig entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        db.AccountTypeConfigs.Update(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
