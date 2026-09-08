using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Nordiska.Modules.Faq.Application;
using Nordiska.Modules.Faq.Contracts.Responses;
using Nordiska.Modules.Faq.Domain;
using Nordiska.Modules.Faq.Infrastructure.Db;

namespace Nordiska.Modules.Faq.Infrastructure;

// This class is a cached wrapper around the FaqRepository, implementing the IFaqRepository interface.
public sealed class CachedFaqRepository : IFaqRepository
{
    private readonly FaqRepository _inner;
    private readonly IMemoryCache _cache;
    private readonly MemoryCacheEntryOptions _options;
    private const string KeyPrefix = "faq:";

    public CachedFaqRepository(FaqRepository inner, IMemoryCache cache, IConfiguration config)
    {
        _inner = inner;
        _cache = cache;
        var ttlSeconds = config.GetValue<int?>("Caching:FaqTtlSeconds") ?? 300;
        _options = new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(ttlSeconds) };
    }

    public async Task<int> CreateAsync(FaqEntry entry, CancellationToken cancellationToken = default)
    {
        var id = await _inner.CreateAsync(entry, cancellationToken);
        // Invalidate cache for this id (if any)
        _cache.Remove(KeyPrefix + id);
        return id;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var ok = await _inner.DeleteAsync(id, cancellationToken);
        if (ok) _cache.Remove(KeyPrefix + id);
        return ok;
    }

    public async Task<FaqEntryResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var key = KeyPrefix + id;
        if (_cache.TryGetValue<FaqEntryResponse?>(key, out var cached))
            return cached;

        var value = await _inner.GetByIdAsync(id, cancellationToken);
        _cache.Set(key, value, _options);
        return value;
    }
}
