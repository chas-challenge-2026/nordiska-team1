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
        db.FaqEntries.Remove(entry);
        await db.SaveChangesAsync(cancellationToken);
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
}