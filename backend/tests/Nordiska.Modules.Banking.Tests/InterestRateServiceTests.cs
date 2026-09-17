using Microsoft.Extensions.Caching.Memory;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Domain;
using Nordiska.Modules.Banking.Infrastructure;

namespace Nordiska.Modules.Banking.Tests;

public class InterestRateServiceTests
{
    private class FakeAccountTypeConfigRepo : IAccountTypeConfigRepository
    {
        private readonly List<AccountTypeConfig> _store;

        public int GetAllCalls { get; private set; }

        public FakeAccountTypeConfigRepo(IEnumerable<AccountTypeConfig> seed)
        {
            _store = seed.ToList();
        }

        public Task<IEnumerable<AccountTypeConfig>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            GetAllCalls++;
            return Task.FromResult<IEnumerable<AccountTypeConfig>>(_store.ToList());
        }
    }

    [Fact]
    public async Task GetAll_ReturnsInterestRatesPerAccountType()
    {
        var repo = new FakeAccountTypeConfigRepo(new[]
        {
            new AccountTypeConfig { AccountType = "saving", InterestRate = 0.035m, Description = "Sparkonto" },
            new AccountTypeConfig { AccountType = "fix", InterestRate = 0.041m, Description = "Fasträntekonto" }
        });
        var service = new InterestRateService(repo, new MemoryCache(new MemoryCacheOptions()), new TestLogger<InterestRateService>());

        var rates = (await service.GetAllAsync()).ToList();

        Assert.Equal(2, rates.Count);
        Assert.Contains(rates, r => r.AccountType == "fix" && r.InterestRate == 0.041m);
    }

    [Fact]
    public async Task GetAll_SecondCall_IsServedFromCache()
    {
        var repo = new FakeAccountTypeConfigRepo(new[]
        {
            new AccountTypeConfig { AccountType = "saving", InterestRate = 0.035m, Description = "Sparkonto" }
        });
        var service = new InterestRateService(repo, new MemoryCache(new MemoryCacheOptions()), new TestLogger<InterestRateService>());

        await service.GetAllAsync();
        await service.GetAllAsync();

        Assert.Equal(1, repo.GetAllCalls);
    }
}
