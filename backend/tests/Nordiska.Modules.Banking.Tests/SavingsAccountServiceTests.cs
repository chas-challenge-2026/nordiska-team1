using System.Threading;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Domain;
using Nordiska.Modules.Banking.Infrastructure;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Contracts.Responses;
using Microsoft.Extensions.Logging;

namespace Nordiska.Modules.Banking.Tests;

public class SavingsAccountServiceTests
{
    private class FakeSavingsRepo : ISavingsAccountRepository
    {
        private readonly List<SavingsAccount> _store = new();
        private long _next = 1;

        public FakeSavingsRepo(IEnumerable<SavingsAccount>? seed = null)
        {
            if (seed != null)
            {
                _store.AddRange(seed);
                _next = (_store.MaxBy(s => s.Id)?.Id ?? 0) + 1;
            }
        }

        public Task<IEnumerable<SavingsAccount>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<SavingsAccount>>(_store.ToList());

        public Task<SavingsAccount?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.FirstOrDefault(s => s.Id == id));

        public Task<int> CreateAsync(SavingsAccount entity, CancellationToken cancellationToken = default)
        {
            entity.Id = _next++;
            _store.Add(entity);
            return Task.FromResult((int)entity.Id);
        }

        public Task UpdateAsync(SavingsAccount entity, CancellationToken cancellationToken = default)
        {
            var idx = _store.FindIndex(s => s.Id == entity.Id);
            if (idx >= 0) _store[idx] = entity;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task GetAll_ReturnsAllAccounts()
    {
        var seed = new[]
        {
            new SavingsAccount { Id = 1, CustomerId = 1, AccountNumber = "A1", AccountType = "Standard", Balance = 100 },
            new SavingsAccount { Id = 2, CustomerId = 2, AccountNumber = "A2", AccountType = "Premium", Balance = 200 }
        };

        var repo = new FakeSavingsRepo(seed);
        var service = new SavingsAccountService(repo, new TestLogger<SavingsAccountService>());

        var all = await service.GetAllAsync();

        Assert.Equal(2, all.Count());
    }

    [Fact]
    public async Task Create_AddsNewAccount_ReturnsResponse()
    {
        var repo = new FakeSavingsRepo();
        var service = new SavingsAccountService(repo, new TestLogger<SavingsAccountService>());

        var req = new OpenSavingsAccountRequest(1, "SE1234", "Standard", 500m, 0.5m);

        var created = await service.CreateAsync(req);

        Assert.NotNull(created);
        Assert.True(created.Id > 0);
        Assert.Equal(req.CustomerId, created.CustomerId);
        Assert.Equal(req.AccountNumber, created.AccountNumber);
        Assert.Equal(req.InitialDeposit, created.Balance);
    }
}
