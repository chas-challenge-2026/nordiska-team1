using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Contracts.Mappers;
using Nordiska.Modules.Banking.Contracts.Responses;

namespace Nordiska.Modules.Banking.Infrastructure;

public class InterestRateService : IInterestRateService
{
    private const string CacheKey = "interest-rates:all";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    private readonly IAccountTypeConfigRepository _repo;
    private readonly IMemoryCache _cache;
    private readonly ILogger<InterestRateService> _logger;

    public InterestRateService(IAccountTypeConfigRepository repo, IMemoryCache cache, ILogger<InterestRateService> logger)
    {
        _repo = repo;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IEnumerable<AccountTypeConfigResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(CacheKey, out List<AccountTypeConfigResponse>? cached) && cached is not null)
        {
            return cached;
        }

        var configs = await _repo.GetAllAsync(cancellationToken);
        var rates = configs.Select(c => c.ToResponse()).ToList();

        _cache.Set(CacheKey, rates, CacheDuration);
        _logger.LogInformation("Loaded {Count} interest rates from database and cached them for {Duration}", rates.Count, CacheDuration);

        return rates;
    }
}
