using Microsoft.EntityFrameworkCore;
using Nordiska.Modules.Faq.Domain;
using Nordiska.Modules.Faq.Infrastructure.Db;
using Nordiska.Modules.Faq.Application;
using Nordiska.Modules.Faq.Contracts.Requests;
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

    public async Task<IReadOnlyCollection<FaqEntryResponse>> SearchAsync(SearchFaqRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = db.FaqEntries.AsNoTracking().AsQueryable();
        
        // Filter faq by given category 
        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            var categoryTerm = request.Category.Trim().ToLower();
            query = query.Where(entry => entry.Category.ToLower() == categoryTerm);
        }
        
        // Filter faq by specific given keyword 
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim().ToLower();
            query = query.Where(entry => entry.Keywords != null && entry.Keywords.ToLower().Contains(keyword));
        }
        
        // Filter by free text search
        if(!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchTerm = request.SearchTerm.Trim().ToLower();
            query = query.Where(entry => entry.Question.ToLower().Contains(searchTerm) || 
                                         entry.Answer.ToLower().Contains(searchTerm) || 
                                         (entry.Keywords != null && entry.Keywords.ToLower().Contains(searchTerm)));
        }
        
        return await query
            .Select(entry => new FaqEntryResponse(
                entry.Id,
                entry.Question,
                entry.Answer,
                entry.Category,
                entry.HelpfulCount,
                entry.Keywords))
            .ToListAsync(cancellationToken);
    }
}