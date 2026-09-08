using Microsoft.EntityFrameworkCore;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Domain;
using Nordiska.Modules.Banking.Infrastructure.Db;

namespace Nordiska.Modules.Banking.Infrastructure;

public sealed class TransactionRepository(BankingDbContext db) : ITransactionRepository
{
    public Task<IEnumerable<LedgerEntry>> QueryAsync(long? accountId = null, CancellationToken cancellationToken = default)
    {
        var query = db.LedgerEntries.AsNoTracking().AsQueryable();
        if (accountId.HasValue) query = query.Where(l => l.AccountId == accountId.Value);
        return query.ToListAsync(cancellationToken).ContinueWith(t => (IEnumerable<LedgerEntry>)t.Result, cancellationToken);
    }

    public Task<LedgerEntry?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => db.LedgerEntries.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    public async Task<int> CreateAsync(LedgerEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.Id != 0) throw new ArgumentException("Only new ledger entries can be created.", nameof(entry));
        db.LedgerEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        return (int)entry.Id;
    }
}
