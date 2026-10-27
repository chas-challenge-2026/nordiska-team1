using Microsoft.EntityFrameworkCore;
using Nordiska.Modules.Faq.Application;
using Nordiska.Modules.Faq.Contracts.Responses;
using Nordiska.Modules.Faq.Domain;
using Nordiska.Modules.Faq.Infrastructure.Db;

namespace Nordiska.Modules.Faq.Infrastructure;

public sealed class FaqSearchLogRepository : IFaqSearchLogRepository
{
    private readonly FaqDbContext _db;

    public FaqSearchLogRepository(FaqDbContext db)
    {
        _db = db;
    }

    public async Task AddRangeAsync(IReadOnlyCollection<FaqSearchLog> logs, CancellationToken cancellationToken = default)
    {
        if (logs.Count == 0)
        {
            return;
        }

        _db.FaqSearchLogs.AddRange(logs);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FaqContentGapResponse>> GetContentGapsAsync(string? language, DateTime since, int limit, CancellationToken cancellationToken = default)
    {
        var query = _db.FaqSearchLogs
            .AsNoTracking()
            .Where(l => l.ResultCount == 0 && l.SearchedAt >= since);

        if (!string.IsNullOrWhiteSpace(language))
        {
            var lang = language.Trim().ToLowerInvariant();
            query = query.Where(l => l.Language == lang);
        }

        // EF can't order by properties of a record created through its constructor, so sort on an anonymous type first
        var gaps = await query
            .GroupBy(l => new { l.NormalizedQuery, l.Language })
            .Select(g => new
            {
                g.Key.NormalizedQuery,
                g.Key.Language,
                SearchCount = g.Count(),
                LastSearchedAt = g.Max(l => l.SearchedAt)
            })
            .OrderByDescending(g => g.SearchCount)
            .ThenByDescending(g => g.LastSearchedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return gaps
            .Select(g => new FaqContentGapResponse(g.NormalizedQuery, g.Language, g.SearchCount, g.LastSearchedAt))
            .ToList();
    }

    public async Task<FaqSearchAnalyticsResponse> GetAnalyticsAsync(
        string? language,
        DateTime since,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = _db.FaqSearchLogs.AsNoTracking().Where(log => log.SearchedAt >= since);
        if (!string.IsNullOrWhiteSpace(language))
        {
            var normalizedLanguage = language.Trim().ToLowerInvariant();
            query = query.Where(log => log.Language == normalizedLanguage);
        }

        var total = await query.CountAsync(cancellationToken);
        var successful = await query.CountAsync(log => log.ResultCount > 0, cancellationToken);
        var uniqueQueries = await query
            .Select(log => new { log.NormalizedQuery, log.Language })
            .Distinct()
            .CountAsync(cancellationToken);

        var gaps = await query
            .Where(log => log.ResultCount == 0)
            .GroupBy(log => new { log.NormalizedQuery, log.Language })
            .Select(group => new
            {
                group.Key.NormalizedQuery,
                group.Key.Language,
                SearchCount = group.Count(),
                LastSearchedAt = group.Max(log => log.SearchedAt)
            })
            .OrderByDescending(gap => gap.SearchCount)
            .ThenByDescending(gap => gap.LastSearchedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var failed = total - successful;
        var successRate = total == 0 ? 0 : Math.Round(successful * 100d / total, 1);
        return new FaqSearchAnalyticsResponse(
            total,
            successful,
            failed,
            uniqueQueries,
            successRate,
            gaps.Select(gap => new FaqContentGapResponse(
                gap.NormalizedQuery,
                gap.Language,
                gap.SearchCount,
                gap.LastSearchedAt)).ToList());
    }

    public async Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken = default)
    {
        return await _db.FaqSearchLogs
            .Where(l => l.SearchedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
