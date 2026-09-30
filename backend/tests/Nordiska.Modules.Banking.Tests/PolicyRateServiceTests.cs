using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Infrastructure;
using Nordiska.Modules.Banking.Infrastructure.External.Riksbank;

namespace Nordiska.Modules.Banking.Tests;

public class PolicyRateServiceTests
{
    private class FakeRiksbankClient : IRiksbankClient
    {
        public PolicyRateObservation Observation { get; set; } = new(new DateOnly(2026, 9, 18), 1.75m);
        public Exception? ThrowOnNextCall { get; set; }
        public int Calls { get; private set; }

        public Task<PolicyRateObservation> GetLatestPolicyRateAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            if (ThrowOnNextCall is not null)
            {
                throw ThrowOnNextCall;
            }

            return Task.FromResult(Observation);
        }
    }

    private static PolicyRateService CreateService(FakeRiksbankClient client, IMemoryCache cache, TimeSpan? cacheDuration = null)
    {
        var options = new RiksbankOptions { CacheDuration = cacheDuration ?? TimeSpan.FromHours(6) };
        return new PolicyRateService(client, cache, Options.Create(options), new TestLogger<PolicyRateService>());
    }

    [Fact]
    public async Task Get_ConvertsPercentToFraction()
    {
        var service = CreateService(new FakeRiksbankClient(), new MemoryCache(new MemoryCacheOptions()));

        var policyRate = await service.GetAsync();

        Assert.NotNull(policyRate);
        Assert.Equal(0.0175m, policyRate.Rate);
        Assert.Equal(new DateOnly(2026, 9, 18), policyRate.EffectiveDate);
        Assert.Equal("Sveriges Riksbank", policyRate.Source);
        Assert.False(policyRate.Stale);
    }

    [Fact]
    public async Task Get_SecondCall_IsServedFromCache()
    {
        var client = new FakeRiksbankClient();
        var service = CreateService(client, new MemoryCache(new MemoryCacheOptions()));

        await service.GetAsync();
        await service.GetAsync();

        Assert.Equal(1, client.Calls);
    }

    [Fact]
    public async Task Get_RiksbankDownAfterEarlierSuccess_ReturnsLastKnownAsStale()
    {
        var client = new FakeRiksbankClient();
        // Cache expires right away so the second call has to go to the Riksbank again
        var service = CreateService(client, new MemoryCache(new MemoryCacheOptions()), TimeSpan.FromMilliseconds(1));
        await service.GetAsync();
        await Task.Delay(20);
        client.ThrowOnNextCall = new HttpRequestException("Riksbank is down");

        var policyRate = await service.GetAsync();

        Assert.Equal(2, client.Calls);
        Assert.NotNull(policyRate);
        Assert.Equal(0.0175m, policyRate.Rate);
        Assert.True(policyRate.Stale);
    }

    [Fact]
    public async Task Get_RiksbankDownAndNothingCached_ReturnsNull()
    {
        var client = new FakeRiksbankClient { ThrowOnNextCall = new TaskCanceledException("Timed out") };
        var service = CreateService(client, new MemoryCache(new MemoryCacheOptions()));

        var policyRate = await service.GetAsync();

        Assert.Null(policyRate);
    }

    [Fact]
    public async Task Get_CallerCancels_ThrowsInsteadOfFallingBack()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var client = new FakeRiksbankClient { ThrowOnNextCall = new OperationCanceledException(cts.Token) };
        var service = CreateService(client, new MemoryCache(new MemoryCacheOptions()));

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.GetAsync(cts.Token));
    }
}
