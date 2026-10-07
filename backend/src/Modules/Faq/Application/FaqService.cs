using System.Collections.ObjectModel;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using Nordiska.BuildingBlocks.Database;
using Nordiska.Modules.Faq.Contracts.Requests;
using Nordiska.Modules.Faq.Domain;
using Nordiska.Modules.Faq.Contracts.Responses;

namespace Nordiska.Modules.Faq.Application;

public interface IFaqRepository
{
    Task<FaqEntryResponse?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<FaqEntryResponse>> GetByRelationIdAsync(
        Guid relationId,
        CancellationToken cancellationToken = default);

    Task<int> CreateAsync(
        FaqEntry entry,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<FaqEntryResponse>> SearchAsync(
        SearchFaqRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<FaqEntryResponse>> QueryPagedAsync(
        FaqQueryParameters parameters,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetCategoriesAsync(
        string language,
        CancellationToken cancellationToken = default);

    Task<FaqEntryResponse?> AdjustHelpfulAsync(
        int id,
        int delta,
        CancellationToken cancellationToken = default);

    Task<FaqEntryResponse?> PatchAsync(
        int id,
        PatchFaqRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RelatedFaqResponse>> GetExplicitRelatedAsync(
        Guid relationId,
        string language,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RelatedFaqResponse>> GetPopularInCategoryAsync(
        string language,
        string category,
        int excludeId,
        DateTime viewsSince,
        int count,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetRelatedRelationIdsAsync(
        Guid relationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Guid>> GetExistingRelationIdsAsync(
        IReadOnlyCollection<Guid> relationIds,
        CancellationToken cancellationToken = default);

    Task SetRelatedAsync(
        Guid relationId,
        IReadOnlyList<Guid> relatedRelationIds,
        CancellationToken cancellationToken = default);
}

public sealed class FaqService(
    IFaqRepository repository,
    IMemoryCache cache,
    FaqCacheInvalidator cacheInvalidator,
    FaqSearchLogQueue searchLogQueue,
    IFaqSearchLogRepository searchLogRepository,
    FaqViewLogQueue viewLogQueue)
{
    public const int MaxRelatedFaqs = 5;

    private const int PopularFallbackCount = 3;
    private const int PopularWindowDays = 30;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    public async Task<int> CreateAsync(
        string question,
        string answer,
        string? category = null,
        string? keywords = null,
        string? language = "sv",
        Guid? relationId = null,
        CancellationToken cancellationToken = default)
    {
        var entry = FaqEntry.Create(
            question,
            answer,
            category,
            keywords,
            language,
            relationId);

        var id = await repository.CreateAsync(
            entry,
            cancellationToken);

        cacheInvalidator.Invalidate();
        return id;
    }

    public async Task<bool> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        var deleted = await repository.DeleteAsync(
            id,
            cancellationToken);

        if (deleted)
        {
            cacheInvalidator.Invalidate();
        }

        return deleted;
    }

    public async Task<FaqEntryResponse?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        var cacheKey = $"faq:{id}";
        if (cache.TryGetValue(cacheKey, out FaqEntryResponse? cached))
        {
            return cached;
        }

        var changeToken = cacheInvalidator.GetChangeToken();
        var entry = await repository.GetByIdAsync(id, cancellationToken);

        if (entry is not null)
        {
            cache.Set(cacheKey, entry, CreateEntryOptions(changeToken));
        }

        return entry;
    }

    // lang is null when the caller doesn't care which language the entry is in
    public async Task<FaqEntryResponse?> GetWithRelatedAsync(
        int id,
        string? lang,
        CancellationToken cancellationToken = default)
    {
        var entry = await GetByIdAsync(id, cancellationToken);
        if (entry is null || (lang is not null && entry.Lang != Normalize(lang)))
        {
            return null;
        }

        var related = await GetRelatedAsync(entry, cancellationToken);
        return entry with { RelatedFaqs = related };
    }

    public async Task<IReadOnlyList<Guid>?> GetRelatedRelationIdsAsync(
        Guid relationId,
        CancellationToken cancellationToken = default)
    {
        var versions = await GetByRelationIdAsync(relationId, cancellationToken);
        if (versions.Count == 0)
        {
            return null;
        }

        return await repository.GetRelatedRelationIdsAsync(relationId, cancellationToken);
    }

    // Returns false when the article doesn't exist, invalid related ids throw ArgumentException (400)
    public async Task<bool> SetRelatedAsync(
        Guid relationId,
        IReadOnlyList<Guid> relatedRelationIds,
        CancellationToken cancellationToken = default)
    {
        if (relatedRelationIds.Count > MaxRelatedFaqs)
        {
            throw new ArgumentException($"An article can have at most {MaxRelatedFaqs} related articles.", nameof(relatedRelationIds));
        }

        if (relatedRelationIds.Contains(relationId))
        {
            throw new ArgumentException("An article can't be related to itself.", nameof(relatedRelationIds));
        }

        if (relatedRelationIds.Distinct().Count() != relatedRelationIds.Count)
        {
            throw new ArgumentException("The same article can only be related once.", nameof(relatedRelationIds));
        }

        var existing = await repository.GetExistingRelationIdsAsync(
            relatedRelationIds.Append(relationId).ToList(),
            cancellationToken);

        if (!existing.Contains(relationId))
        {
            return false;
        }

        var unknown = relatedRelationIds.Where(id => !existing.Contains(id)).ToList();
        if (unknown.Count > 0)
        {
            throw new ArgumentException($"Unknown related article(s): {string.Join(", ", unknown)}.", nameof(relatedRelationIds));
        }

        await repository.SetRelatedAsync(relationId, relatedRelationIds, cancellationToken);
        cacheInvalidator.Invalidate();
        return true;
    }

    // Returns false when the entry doesn't exist, the view itself is saved in the background
    public async Task<bool> RegisterViewAsync(
        int id,
        string sessionKey,
        CancellationToken cancellationToken = default)
    {
        var entry = await GetByIdAsync(id, cancellationToken);
        if (entry is null)
        {
            return false;
        }

        viewLogQueue.TryEnqueue(id, sessionKey);
        return true;
    }

    public async Task<IReadOnlyCollection<FaqEntryResponse>> GetByRelationIdAsync(
        Guid relationId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"faq:relation:{relationId}";

        if (cache.TryGetValue(
            cacheKey,
            out IReadOnlyCollection<FaqEntryResponse>? cached) &&
            cached is not null)
        {
            return cached;
        }

        var changeToken = cacheInvalidator.GetChangeToken();

        var result = await repository.GetByRelationIdAsync(
            relationId,
            cancellationToken);

        cache.Set(
            cacheKey,
            result,
            CreateEntryOptions(changeToken));

        return result;
    }

    public async Task<IReadOnlyCollection<FaqEntryResponse>> SearchAsync(
        SearchFaqRequest request,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"faq:search:{Normalize(request.Lang)}|{Normalize(request.SearchTerm)}|{Normalize(request.Category)}|{Normalize(request.Keyword)}";
        if (cache.TryGetValue(cacheKey, out IReadOnlyCollection<FaqEntryResponse>? cached) && cached is not null)
        {
            return cached;
        }

        var changeToken = cacheInvalidator.GetChangeToken();
        var result = await repository.SearchAsync(request, cancellationToken);

        cache.Set(cacheKey, result, CreateEntryOptions(changeToken));
        return result;
    }

    public async Task<PagedResult<FaqEntryResponse>> GetByLanguagePagedAsync(
        FaqQueryParameters parameters,
        string? sessionKey = null,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"faq:paged:{Normalize(parameters.Lang)}|{parameters.Page}|{parameters.PageSize}|{Normalize(parameters.SearchTerm)}|{Normalize(parameters.Category)}|{Normalize(parameters.Keyword)}";
        if (cache.TryGetValue(cacheKey, out PagedResult<FaqEntryResponse>? cached) && cached is not null)
        {
            LogSearch(parameters, cached.TotalCount, sessionKey);
            return cached;
        }

        var changeToken = cacheInvalidator.GetChangeToken();
        var result = await repository.QueryPagedAsync(parameters, cancellationToken);

        cache.Set(cacheKey, result, CreateEntryOptions(changeToken));
        LogSearch(parameters, result.TotalCount, sessionKey);
        return result;
    }

    public async Task<IReadOnlyList<FaqContentGapResponse>> GetContentGapsAsync(
        string? language,
        int days,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var since = DateTime.UtcNow.AddDays(-days);
        return await searchLogRepository.GetContentGapsAsync(language, since, limit, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(
        string language,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"faq:categories:{Normalize(language)}";
        if (cache.TryGetValue(cacheKey, out IReadOnlyList<string>? cached) && cached is not null)
        {
            return cached;
        }

        var changeToken = cacheInvalidator.GetChangeToken();
        var result = await repository.GetCategoriesAsync(language, cancellationToken);

        cache.Set(cacheKey, result, CreateEntryOptions(changeToken));
        return result;
    }

    public async Task<FaqEntryResponse?> AdjustHelpfulAsync(
        int id,
        int delta,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        var result = await repository.AdjustHelpfulAsync(id, delta, cancellationToken);
        if (result is not null)
        {
            cacheInvalidator.Invalidate();
        }

        return result;
    }

    public async Task<FaqEntryResponse?> PatchAsync(
        int id,
        PatchFaqRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        var result = await repository.PatchAsync(id, request, cancellationToken);
        if (result is not null)
        {
            cacheInvalidator.Invalidate();
        }

        return result;
    }

    // Explicit relations win, the popular fallback is only used when none of them exist in this language
    private async Task<IReadOnlyList<RelatedFaqResponse>> GetRelatedAsync(
        FaqEntryResponse entry,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"faq:related:{entry.Id}";
        if (cache.TryGetValue(cacheKey, out IReadOnlyList<RelatedFaqResponse>? cached) && cached is not null)
        {
            return cached;
        }

        var changeToken = cacheInvalidator.GetChangeToken();
        var related = await repository.GetExplicitRelatedAsync(entry.RelationId, entry.Lang, cancellationToken);

        if (related.Count == 0 && !string.IsNullOrWhiteSpace(entry.Category))
        {
            related = await repository.GetPopularInCategoryAsync(
                entry.Lang,
                entry.Category,
                entry.Id,
                DateTime.UtcNow.AddDays(-PopularWindowDays),
                PopularFallbackCount,
                cancellationToken);
        }

        cache.Set(cacheKey, related, CreateEntryOptions(changeToken));
        return related;
    }

    // Only the first page counts as a search, the next pages are the same search scrolled further
    private void LogSearch(FaqQueryParameters parameters, int resultCount, string? sessionKey)
    {
        if (sessionKey is null || parameters.Page != 1 || string.IsNullOrWhiteSpace(parameters.SearchTerm))
        {
            return;
        }

        searchLogQueue.TryEnqueue(parameters.SearchTerm, parameters.Lang ?? "sv", resultCount, sessionKey);
    }

    private static MemoryCacheEntryOptions CreateEntryOptions(IChangeToken changeToken)
        => new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(CacheDuration)
            .AddExpirationToken(changeToken);

    private static string Normalize(string? value)
        => value?.Trim().ToLowerInvariant() ?? string.Empty;
}