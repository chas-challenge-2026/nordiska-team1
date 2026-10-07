using Microsoft.EntityFrameworkCore;
using Nordiska.Modules.Faq.Application;
using Nordiska.Modules.Faq.Domain;
using Nordiska.Modules.Faq.Infrastructure.Db;

namespace Nordiska.Modules.Faq.Infrastructure;

public sealed class FaqViewLogRepository : IFaqViewLogRepository
{
    private readonly FaqDbContext _db;

    public FaqViewLogRepository(FaqDbContext db)
    {
        _db = db;
    }

    public async Task AddRangeAsync(IReadOnlyCollection<FaqViewLog> logs, CancellationToken cancellationToken = default)
    {
        if (logs.Count == 0)
        {
            return;
        }

        _db.FaqViewLogs.AddRange(logs);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken = default)
    {
        return await _db.FaqViewLogs
            .Where(l => l.ViewedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
