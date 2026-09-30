using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Nordiska.BuildingBlocks.Database.Errors;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Domain;
using Nordiska.Modules.Banking.Infrastructure;
using Xunit;

namespace Nordiska.Modules.Banking.Tests;

public class AccountTypeConfigServiceTests
{
    private sealed class FakeAccountTypeConfigRepo : IAccountTypeConfigRepository
    {
        private readonly List<AccountTypeConfig> _store = new();

        public int GetAllCalls { get; private set; }

        public FakeAccountTypeConfigRepo(IEnumerable<AccountTypeConfig>? initial = null)
        {
            if (initial != null) _store.AddRange(initial);
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
    public async Task GetAll_ReturnsAllConfigurations_AndCachesResult()
    {
        var seed = new[]
        {
            new AccountTypeConfig { AccountType = "flex", InterestRate = 0.035m, Description = "Flexible" },
            new AccountTypeConfig { AccountType = "fix", InterestRate = 0.041m, Description = "Fixed" }
        };

        var repo = new FakeAccountTypeConfigRepo(seed);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new AccountTypeConfigService(repo, cache, new TestLogger<AccountTypeConfigService>());

        var first = (await service.GetAllAsync()).ToList();
        var second = (await service.GetAllAsync()).ToList();

        Assert.Equal(2, first.Count);
        Assert.Equal(2, second.Count);
        Assert.Equal(1, repo.GetAllCalls); // Cached on second call
    }

    [Fact]
    public async Task Create_WithPercentageInterestRate_NormalizesToDecimal_AndInvalidatesCache()
    {
        var repo = new FakeAccountTypeConfigRepo();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new AccountTypeConfigService(repo, cache, new TestLogger<AccountTypeConfigService>());

        // Populate cache
        await service.GetAllAsync();
        Assert.Equal(1, repo.GetAllCalls);

        var req = new CreateAccountTypeConfigRequest("student", 3.5m, "Student account");
        var result = await service.CreateAsync(req);

        Assert.Equal("student", result.AccountType);
        Assert.Equal(0.035m, result.InterestRate);

        // Next GetAll should fetch from DB again because cache was invalidated
        var afterCreate = (await service.GetAllAsync()).ToList();
        Assert.Equal(2, repo.GetAllCalls);
        Assert.Contains(afterCreate, x => x.AccountType == "student");
    }

    [Fact]
    public async Task Update_UpdatesRateAndDescription_AndInvalidatesCache()
    {
        var seed = new[]
        {
            new AccountTypeConfig { AccountType = "flex", InterestRate = 0.035m, Description = "Flexible" }
        };

        var repo = new FakeAccountTypeConfigRepo(seed);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new AccountTypeConfigService(repo, cache, new TestLogger<AccountTypeConfigService>());

        // Populate cache
        await service.GetAllAsync();
        Assert.Equal(1, repo.GetAllCalls);

        var req = new UpdateAccountTypeConfigRequest("flex", 4.0m, "Updated Description");
        var updated = await service.UpdateAsync("flex", req);

        Assert.Equal("flex", updated.AccountType);
        Assert.Equal(0.040m, updated.InterestRate);
        Assert.Equal("Updated Description", updated.Description);

        // Next GetAll should fetch from DB again because cache was invalidated
        var afterUpdate = (await service.GetAllAsync()).ToList();
        Assert.Equal(2, repo.GetAllCalls);
        Assert.Contains(afterUpdate, x => x.AccountType == "flex" && x.InterestRate == 0.040m);
    }

    [Fact]
    public async Task GetByType_NotFound_ThrowsNotFoundException()
    {
        var repo = new FakeAccountTypeConfigRepo();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new AccountTypeConfigService(repo, cache, new TestLogger<AccountTypeConfigService>());

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByTypeAsync("nonexistent"));
    }

    private sealed class FakeSavingsAccountRepo : ISavingsAccountRepository
    {
        private readonly List<SavingsAccount> _store = new();
        private long _next = 1;

        public Task<IEnumerable<SavingsAccount>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<SavingsAccount>>(_store.ToList());

        public Task<IEnumerable<SavingsAccount>> GetByCustomerIdAsync(long customerId, CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<SavingsAccount>>(_store.Where(s => s.CustomerId == customerId).ToList());

        public Task<SavingsAccount?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.FirstOrDefault(s => s.Id == id));

        public Task<long> CreateAsync(SavingsAccount entity, CancellationToken cancellationToken = default)
        {
            entity.Id = _next++;
            _store.Add(entity);
            return Task.FromResult(entity.Id);
        }

        public Task UpdateAsync(SavingsAccount entity, CancellationToken cancellationToken = default)
        {
            var idx = _store.FindIndex(s => s.Id == entity.Id);
            if (idx >= 0) _store[idx] = entity;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task SavingsAccountService_AutoAssignsRateFromConfig()
    {
        var configRepo = new FakeAccountTypeConfigRepo(new[]
        {
            new AccountTypeConfig { AccountType = "flex", InterestRate = 0.0350m, Description = "Flex" }
        });
        var savingsRepo = new FakeSavingsAccountRepo();
        var service = new SavingsAccountService(savingsRepo, new TestLogger<SavingsAccountService>(), configRepo);

        var req = new OpenSavingsAccountRequest(1, "NOR-112233", "flex", 1000m, null);
        var created = await service.CreateAsync(req);

        Assert.Equal("flex", created.AccountType);
        Assert.Equal(0.0350m, created.InterestRate);
    }
}
