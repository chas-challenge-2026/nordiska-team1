using Microsoft.EntityFrameworkCore;
using Nordiska.Modules.Faq.Domain;
using Nordiska.Modules.Faq.Infrastructure.Db;
using Nordiska.Modules.Faq.Application;
using Nordiska.Modules.Faq.Contracts.Responses;
namespace Nordiska.Modules.Faq.Infrastructure;

public sealed class FaqRepository(FaqDbContext db) : IFaqRepository
{
    public async Task<int> CreateAsync
    (
        FaqEntry entry,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.Id != 0)
        {
            throw new ArgumentException("Only a new entry can be created. This entry already exists: ", nameof(entry));
        }

        db.FaqEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        return entry.Id;
    }

 

    public async Task<bool> DeleteAsync
    (
        int id,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        var entry = await db.FaqEntries.SingleOrDefaultAsync(
            entry => entry.Id == id,
            cancellationToken);
        if (entry is null)
        {
            return false;
        }
        db.FaqEntries.Remove(entry);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<FaqEntryResponse?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return db.FaqEntries
            .AsNoTracking()
            .Where(entry => entry.Id == id)
            .Select(entry => new FaqEntryResponse(
                entry.Id,
                entry.Question,
                entry.Answer,
                entry.Category,
                entry.HelpfulCount,
                entry.Keywords))
            .SingleOrDefaultAsync(cancellationToken);
    }
}