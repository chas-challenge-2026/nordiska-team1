using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Domain;
using Nordiska.Modules.Banking.Infrastructure;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Contracts.Responses;
using Microsoft.Extensions.Logging;

namespace Nordiska.Modules.Banking.Tests;

public class TransactionServiceTests
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

    private class FakeTxRepo : ITransactionRepository
    {
        private readonly List<LedgerEntry> _store = new();
        private long _next = 1;

        public FakeTxRepo(IEnumerable<LedgerEntry>? seed = null)
        {
            if (seed != null)
            {
                _store.AddRange(seed);
                _next = (_store.MaxBy(t => t.Id)?.Id ?? 0) + 1;
            }
        }

        public Task<IEnumerable<LedgerEntry>> QueryAsync(long? accountId = null, CancellationToken cancellationToken = default)
        {
            var q = _store.AsEnumerable();
            if (accountId.HasValue) q = q.Where(l => l.AccountId == accountId.Value);
            return Task.FromResult<IEnumerable<LedgerEntry>>(q.ToList());
        }

        public Task<LedgerEntry?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.FirstOrDefault(l => l.Id == id));

        public Task<int> CreateAsync(LedgerEntry entry, CancellationToken cancellationToken = default)
        {
            entry.Id = _next++;
            _store.Add(entry);
            return Task.FromResult((int)entry.Id);
        }
    }

    [Fact]
    public async Task Query_ReturnsFilteredTransactions()
    {
        var seed = new[]
        {
            new LedgerEntry { Id = 1, AccountId = 1, Type = "deposit", Amount = 100, CreatedAt = DateTime.UtcNow },
            new LedgerEntry { Id = 2, AccountId = 2, Type = "withdrawal", Amount = 50, CreatedAt = DateTime.UtcNow }
        };

        var txRepo = new FakeTxRepo(seed);
        var accRepo = new FakeSavingsRepo();
        var svc = new TransactionService(txRepo, accRepo, new TestLogger<TransactionService>());

        var res = await svc.QueryAsync(1);

        Assert.Single(res);
        Assert.Equal(1, res.First().AccountId);
    }

    [Fact]
    public async Task Execute_Deposit_UpdatesAccountAndCreatesEntry()
    {
        var acc = new SavingsAccount { Id = 1, CustomerId = 1, AccountNumber = "A1", Balance = 100m };
        var accRepo = new FakeSavingsRepo(new[] { acc });
        var txRepo = new FakeTxRepo();
        var svc = new TransactionService(txRepo, accRepo, new TestLogger<TransactionService>());

        var req = new TransactionRequest(1, "deposit", 50m);
        var res = await svc.ExecuteAsync(req);

        Assert.Equal(150m, (await accRepo.GetByIdAsync(1))!.Balance);
        Assert.Equal(1, res.AccountId);
        Assert.Equal(50m, res.Amount);
    }

    [Fact]
    public async Task Execute_Withdrawal_Insufficient_Throws()
    {
        var acc = new SavingsAccount { Id = 1, CustomerId = 1, AccountNumber = "A1", Balance = 10m };
        var accRepo = new FakeSavingsRepo(new[] { acc });
        var txRepo = new FakeTxRepo();
        var svc = new TransactionService(txRepo, accRepo, new TestLogger<TransactionService>());

        var req = new TransactionRequest(1, "withdrawal", 50m);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ExecuteAsync(req));
    }
}
