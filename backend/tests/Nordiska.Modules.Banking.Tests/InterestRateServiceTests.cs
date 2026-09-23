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

        public Task<AccountTypeConfig?> GetByTypeAsync(string accountType, CancellationToken cancellationToken = default)
        {
            var match = _store.FirstOrDefault(x => string.Equals(x.AccountType, accountType, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(match);
        }

        public Task CreateAsync(AccountTypeConfig entity, CancellationToken cancellationToken = default)
        {
            _store.Add(entity);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(AccountTypeConfig entity, CancellationToken cancellationToken = default)
        {
            var idx = _store.FindIndex(x => string.Equals(x.AccountType, entity.AccountType, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) _store[idx] = entity;
            return Task.CompletedTask;
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
        var configService = new AccountTypeConfigService(repo, new MemoryCache(new MemoryCacheOptions()), new TestLogger<AccountTypeConfigService>());
        var service = new InterestRateService(configService);

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
        var configService = new AccountTypeConfigService(repo, new MemoryCache(new MemoryCacheOptions()), new TestLogger<AccountTypeConfigService>());
        var service = new InterestRateService(configService);

        await service.GetAllAsync();
        await service.GetAllAsync();

        Assert.Equal(1, repo.GetAllCalls);
    }
}
