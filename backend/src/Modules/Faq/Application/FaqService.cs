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
}

public sealed class FaqService(IFaqRepository repository, IMemoryCache cache, FaqCacheInvalidator cacheInvalidator)
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    public async Task<int> CreateAsync(
        string question,
        string answer,
        string? category = null,
        string? keywords = null,
        string? language = "sv",
        CancellationToken cancellationToken = default)
    {
        var entry = FaqEntry.Create(
            question,
            answer,
            category,
            keywords,
            language);

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
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"faq:paged:{Normalize(parameters.Lang)}|{parameters.Page}|{parameters.PageSize}|{Normalize(parameters.SearchTerm)}|{Normalize(parameters.Category)}|{Normalize(parameters.Keyword)}";
        if (cache.TryGetValue(cacheKey, out PagedResult<FaqEntryResponse>? cached) && cached is not null)
        {
            return cached;
        }

        var changeToken = cacheInvalidator.GetChangeToken();
        var result = await repository.QueryPagedAsync(parameters, cancellationToken);

        cache.Set(cacheKey, result, CreateEntryOptions(changeToken));
        return result;
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

    private static MemoryCacheEntryOptions CreateEntryOptions(IChangeToken changeToken)
        => new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(CacheDuration)
            .AddExpirationToken(changeToken);

    private static string Normalize(string? value)
        => value?.Trim().ToLowerInvariant() ?? string.Empty;
}