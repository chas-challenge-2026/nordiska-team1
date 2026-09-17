using System.Collections.ObjectModel;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
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
    Task<IReadOnlyCollection<FaqEntryResponse>> SearchAsync(SearchFaqRequest request,
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
        CancellationToken cancellationToken = default)
    {
        var entry = FaqEntry.Create(
            question,
            answer,
            category,
            keywords);

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

        // Take the change token before reading so a write during the read evicts this entry
        var changeToken = cacheInvalidator.GetChangeToken();
        var entry = await repository.GetByIdAsync(id, cancellationToken);

        // Missing entries are not cached to avoid filling the cache with unknown ids
        if (entry is not null)
        {
            cache.Set(cacheKey, entry, CreateEntryOptions(changeToken));
        }

        return entry;
    }

    public async Task<IReadOnlyCollection<FaqEntryResponse>> SearchAsync(SearchFaqRequest request,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"faq:search:{Normalize(request.SearchTerm)}|{Normalize(request.Category)}|{Normalize(request.Keyword)}";
        if (cache.TryGetValue(cacheKey, out IReadOnlyCollection<FaqEntryResponse>? cached) && cached is not null)
        {
            return cached;
        }

        var changeToken = cacheInvalidator.GetChangeToken();
        var result = await repository.SearchAsync(request, cancellationToken);

        cache.Set(cacheKey, result, CreateEntryOptions(changeToken));
        return result;
    }

    private static MemoryCacheEntryOptions CreateEntryOptions(IChangeToken changeToken)
        => new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(CacheDuration)
            .AddExpirationToken(changeToken);

    // Search is case-insensitive and trims input, so the cache key does the same
    private static string Normalize(string? value)
        => value?.Trim().ToLowerInvariant() ?? string.Empty;
}
