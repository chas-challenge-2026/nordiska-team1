using Microsoft.EntityFrameworkCore;
using Nordiska.Modules.Faq.Contracts.Responses;
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

    public async Task<FaqViewAnalyticsResponse> GetAnalyticsAsync(
        string? language,
        DateTime since,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query =
            from view in _db.FaqViewLogs.AsNoTracking()
            join entry in _db.FaqEntries.AsNoTracking() on view.FaqEntryId equals entry.Id
            where view.ViewedAt >= since
            select new { view, entry };

        if (!string.IsNullOrWhiteSpace(language))
        {
            var normalizedLanguage = language.Trim().ToLowerInvariant();
            query = query.Where(row => row.entry.Language.ToLower() == normalizedLanguage);
        }

        var totalViews = await query.CountAsync(cancellationToken);
        var popular = await query
            .GroupBy(row => new { row.entry.Id, row.entry.Question, row.entry.Category, row.entry.Language })
            .Select(group => new
            {
                group.Key.Id,
                group.Key.Question,
                group.Key.Category,
                Lang = group.Key.Language,
                ViewCount = group.Count()
            })
            .OrderByDescending(article => article.ViewCount)
            .ThenBy(article => article.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return new FaqViewAnalyticsResponse(
            totalViews,
            popular.Select(article => new FaqPopularArticleResponse(
                article.Id,
                article.Question,
                article.Category,
                article.Lang,
                article.ViewCount)).ToList());
    }
}
