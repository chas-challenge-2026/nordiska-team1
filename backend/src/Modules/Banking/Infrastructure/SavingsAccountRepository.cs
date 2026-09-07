using Microsoft.EntityFrameworkCore;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Domain;
using Nordiska.Modules.Banking.Infrastructure.Db;

namespace Nordiska.Modules.Banking.Infrastructure;

public sealed class SavingsAccountRepository(BankingDbContext db) : ISavingsAccountRepository
{
    public Task<IEnumerable<SavingsAccount>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return db.SavingsAccounts.AsNoTracking().ToListAsync(cancellationToken)
            .ContinueWith(t => (IEnumerable<SavingsAccount>)t.Result, cancellationToken);
    }

    public Task<SavingsAccount?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => db.SavingsAccounts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<int> CreateAsync(SavingsAccount entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (entity.Id != 0) throw new ArgumentException("Only new entity can be created.", nameof(entity));
        db.SavingsAccounts.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return (int)entity.Id;
    }

    public async Task UpdateAsync(SavingsAccount entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        db.SavingsAccounts.Update(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
