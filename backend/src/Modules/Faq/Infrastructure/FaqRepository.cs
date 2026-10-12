using Microsoft.EntityFrameworkCore;
using Nordiska.BuildingBlocks.Database;
using Nordiska.Modules.Faq.Domain;
using Nordiska.Modules.Faq.Infrastructure.Db;
using Nordiska.Modules.Faq.Application;
using Nordiska.Modules.Faq.Contracts.Mappers;
using Nordiska.Modules.Faq.Contracts.Requests;
using Nordiska.Modules.Faq.Contracts.Responses;

namespace Nordiska.Modules.Faq.Infrastructure;

public sealed class FaqRepository(FaqDbContext db) : IFaqRepository
{
    public async Task<int> CreateAsync(
        FaqEntry entry,
        CancellationToken cancellationToken = default)
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

    public async Task<bool> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        var entry = await db.FaqEntries.SingleOrDefaultAsync(
            entry => entry.Id == id,
            cancellationToken);
        if (entry is null)
        {
            return false;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        db.FaqEntries.Remove(entry);
        await db.SaveChangesAsync(cancellationToken);

        // Relations belong to the article, so they're only removed when its last language version is gone
        var hasOtherVersions = await db.FaqEntries.AnyAsync(e => e.RelationId == entry.RelationId, cancellationToken);
        if (!hasOtherVersions)
        {
            await db.FaqRelationships
                .Where(r => r.RelationId == entry.RelationId || r.RelatedRelationId == entry.RelationId)
                .ExecuteDeleteAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<FaqEntryResponse?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var entry = await db.FaqEntries
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.Id == id, cancellationToken);

        return entry?.ToResponse();
    }

    public async Task<IReadOnlyCollection<FaqEntryResponse>> SearchAsync(
        SearchFaqRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = db.FaqEntries.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Lang))
        {
            var lang = request.Lang.Trim().ToLowerInvariant();
            query = query.Where(entry => entry.Language.ToLower() == lang);
        }

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            var categoryTerm = request.Category.Trim().ToLowerInvariant();
            query = query.Where(entry => entry.Category.ToLower() == categoryTerm);
        }

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim().ToLowerInvariant();
            query = query.Where(entry => entry.Keywords != null && entry.Keywords.ToLower().Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchTerm = request.SearchTerm.Trim().ToLowerInvariant();
            query = query.Where(entry => entry.Question.ToLower().Contains(searchTerm) ||
                                         entry.Answer.ToLower().Contains(searchTerm) ||
                                         (entry.Keywords != null && entry.Keywords.ToLower().Contains(searchTerm)));
        }

        var entries = await query.ToListAsync(cancellationToken);
        return entries.Select(e => e.ToResponse()).ToList();
    }

    public async Task<PagedResult<FaqEntryResponse>> QueryPagedAsync(
        FaqQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var query = db.FaqEntries.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(parameters.Lang))
        {
            var lang = parameters.Lang.Trim().ToLowerInvariant();
            query = query.Where(e => e.Language.ToLower() == lang);
        }

        if (!string.IsNullOrWhiteSpace(parameters.Category))
        {
            var category = parameters.Category.Trim().ToLowerInvariant();
            query = query.Where(e => e.Category.ToLower() == category);
        }

        if (!string.IsNullOrWhiteSpace(parameters.Keyword))
        {
            var kw = parameters.Keyword.Trim().ToLowerInvariant();
            query = query.Where(e => e.Keywords != null && e.Keywords.ToLower().Contains(kw));
        }

        if (!string.IsNullOrWhiteSpace(parameters.SearchTerm))
        {
            var search = parameters.SearchTerm.Trim().ToLowerInvariant();
            query = query.Where(e => e.Question.ToLower().Contains(search) ||
                                     e.Answer.ToLower().Contains(search) ||
                                     (e.Keywords != null && e.Keywords.ToLower().Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var page = parameters.Page < 1 ? 1 : parameters.Page;
        var pageSize = parameters.PageSize < 1 ? 20 : (parameters.PageSize > 100 ? 100 : parameters.PageSize);

        var entries = await query
            .OrderBy(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var mapped = entries.Select(e => e.ToResponse()).ToList();
        return PagedResult<FaqEntryResponse>.Create(mapped, totalCount, page, pageSize);
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(
        string language,
        CancellationToken cancellationToken = default)
    {
        var lang = string.IsNullOrWhiteSpace(language) ? "sv" : language.Trim().ToLowerInvariant();
        var categories = await db.FaqEntries
            .AsNoTracking()
            .Where(e => e.Language.ToLower() == lang && !string.IsNullOrEmpty(e.Category))
            .Select(e => e.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(cancellationToken);

        return categories;
    }

    public async Task<FaqEntryResponse?> AdjustHelpfulAsync(
        int id,
        int delta,
        CancellationToken cancellationToken = default)
    {
        var entry = await db.FaqEntries.SingleOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (entry is null)
        {
            return null;
        }

        if (delta > 0)
        {
            entry.MarkHelpful();
        }
        else if (delta < 0)
        {
            entry.UnmarkHelpful();
        }

        await db.SaveChangesAsync(cancellationToken);
        return entry.ToResponse();
    }

    public async Task<FaqEntryResponse?> PatchAsync(
        int id,
        PatchFaqRequest request,
        CancellationToken cancellationToken = default)
    {
        var entry = await db.FaqEntries.SingleOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (entry is null)
        {
            return null;
        }

        entry.ApplyPatch(request);
        await db.SaveChangesAsync(cancellationToken);
        return entry.ToResponse();
    }

    public async Task<IReadOnlyCollection<FaqEntryResponse>> GetByRelationIdAsync(
        Guid relationId,
        CancellationToken cancellationToken = default)
    {
        var entries = await db.FaqEntries
            .AsNoTracking()
            .Where(e => e.RelationId == relationId)
            .ToListAsync(cancellationToken);

        return entries
            .Select(e => e.ToResponse())
            .ToList();
    }

    public async Task<IReadOnlyList<RelatedFaqResponse>> GetExplicitRelatedAsync(
        Guid relationId,
        string language,
        CancellationToken cancellationToken = default)
    {
        // Inner join drops related articles that don't have a version in this language
        var related = await db.FaqRelationships
            .AsNoTracking()
            .Where(r => r.RelationId == relationId)
            .Join(
                db.FaqEntries.Where(e => e.Language == language),
                r => r.RelatedRelationId,
                e => e.RelationId,
                (r, e) => new { r.SortOrder, e.Id, e.Question, e.Category })
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);

        return related
            .Select(x => new RelatedFaqResponse(x.Id, x.Question, x.Category))
            .ToList();
    }

    public async Task<IReadOnlyList<RelatedFaqResponse>> GetPopularInCategoryAsync(
        string language,
        string category,
        int excludeId,
        DateTime viewsSince,
        int count,
        CancellationToken cancellationToken = default)
    {
        // Helpful count and age break ties, so it still gives a sensible order before anything has been viewed
        var popular = await db.FaqEntries
            .AsNoTracking()
            .Where(e => e.Language == language && e.Category == category && e.Id != excludeId)
            .Select(e => new
            {
                e.Id,
                e.Question,
                e.Category,
                e.HelpfulCount,
                e.CreatedAt,
                Views = db.FaqViewLogs.Count(v => v.FaqEntryId == e.Id && v.ViewedAt >= viewsSince)
            })
            .OrderByDescending(x => x.Views)
            .ThenByDescending(x => x.HelpfulCount)
            .ThenByDescending(x => x.CreatedAt)
            .Take(count)
            .ToListAsync(cancellationToken);

        return popular
            .Select(x => new RelatedFaqResponse(x.Id, x.Question, x.Category))
            .ToList();
    }

    public async Task<IReadOnlyList<Guid>> GetRelatedRelationIdsAsync(
        Guid relationId,
        CancellationToken cancellationToken = default)
    {
        return await db.FaqRelationships
            .AsNoTracking()
            .Where(r => r.RelationId == relationId)
            .OrderBy(r => r.SortOrder)
            .Select(r => r.RelatedRelationId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Guid>> GetExistingRelationIdsAsync(
        IReadOnlyCollection<Guid> relationIds,
        CancellationToken cancellationToken = default)
    {
        return await db.FaqEntries
            .AsNoTracking()
            .Where(e => relationIds.Contains(e.RelationId))
            .Select(e => e.RelationId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task SetRelatedAsync(
        Guid relationId,
        IReadOnlyList<Guid> relatedRelationIds,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        await db.FaqRelationships
            .Where(r => r.RelationId == relationId)
            .ExecuteDeleteAsync(cancellationToken);

        db.FaqRelationships.AddRange(relatedRelationIds
            .Select((relatedId, index) => FaqRelationship.Create(relationId, relatedId, index)));

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}