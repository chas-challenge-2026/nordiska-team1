using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Contracts.Responses;
using Nordiska.Modules.Banking.Infrastructure.External.Riksbank;

namespace Nordiska.Modules.Banking.Infrastructure;

public class PolicyRateService : IPolicyRateService
{
    private const string CacheKey = "policy-rate:latest";
    private const string LastKnownCacheKey = "policy-rate:last-known";
    private const string Source = "Sveriges Riksbank";

    private readonly IRiksbankClient _riksbankClient;
    private readonly IMemoryCache _cache;
    private readonly RiksbankOptions _options;
    private readonly ILogger<PolicyRateService> _logger;

    public PolicyRateService(
        IRiksbankClient riksbankClient,
        IMemoryCache cache,
        IOptions<RiksbankOptions> options,
        ILogger<PolicyRateService> logger)
    {
        _riksbankClient = riksbankClient;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PolicyRateResponse?> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(CacheKey, out PolicyRateResponse? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            var observation = await _riksbankClient.GetLatestPolicyRateAsync(cancellationToken);

            // Riksbank publishes the rate in percent, the rest of the API uses fractions (0.035 = 3.5 %)
            var policyRate = new PolicyRateResponse(observation.ValuePercent / 100m, observation.Date, Source, Stale: false);

            _cache.Set(CacheKey, policyRate, _options.CacheDuration);
            // Kept without expiry so there is always something to fall back on if the Riksbank is down
            _cache.Set(LastKnownCacheKey, policyRate, new MemoryCacheEntryOptions { Priority = CacheItemPriority.NeverRemove });
            _logger.LogInformation("Fetched policy rate {Rate} ({EffectiveDate}) from Riksbank and cached it for {Duration}", policyRate.Rate, policyRate.EffectiveDate, _options.CacheDuration);

            return policyRate;
        }
        // Any failure from the external call (HTTP errors, timeouts, open circuit, bad JSON) falls back to the last known value.
        // A cancelled request from the caller is not a Riksbank failure though, so that one is let through.
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            if (_cache.TryGetValue(LastKnownCacheKey, out PolicyRateResponse? lastKnown) && lastKnown is not null)
            {
                _logger.LogWarning(ex, "Could not fetch policy rate from Riksbank, returning last known value from {EffectiveDate}", lastKnown.EffectiveDate);
                return lastKnown with { Stale = true };
            }

            _logger.LogError(ex, "Could not fetch policy rate from Riksbank and there is no cached value to fall back on");
            return null;
        }
    }
}
