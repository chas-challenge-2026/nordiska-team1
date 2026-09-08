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

        public Task<long> CreateAsync(LedgerEntry entry, CancellationToken cancellationToken = default)
        {
            entry.Id = _next++;
            _store.Add(entry);
            return Task.FromResult(entry.Id);
        }

        public int Count => _store.Count;
    }

    [Fact]
    public async Task Query_ReturnsFilteredTransactions()
    {
        var seed = new[]
        {
            new LedgerEntry { Id = 1, AccountId = 1, Type = "deposit", Amount = 100, CreatedAt = DateTime.UtcNow },
            new LedgerEntry { Id = 2, AccountId = 2, Type = "withdrawal", Amount = -50, CreatedAt = DateTime.UtcNow }
        };

        var txRepo = new FakeTxRepo(seed);
        var accRepo = new FakeSavingsRepo();
        var svc = new TransactionService(txRepo, accRepo, new TestLogger<TransactionService>());

        var res = await svc.QueryAsync(1);

        Assert.Single(res);
        Assert.Equal(1, res.First().AccountId);
    }

    [Fact]
    public async Task GetBalance_ReturnsSumOfAllLedgerEntries()
    {
        var acc = new SavingsAccount { Id = 1, CustomerId = 1, AccountNumber = "SE100" };
        var seedTx = new[]
        {
            new LedgerEntry { Id = 1, AccountId = 1, Type = "deposit", Amount = 1000m, CreatedAt = DateTime.UtcNow },
            new LedgerEntry { Id = 2, AccountId = 1, Type = "withdrawal", Amount = -250m, CreatedAt = DateTime.UtcNow },
            new LedgerEntry { Id = 3, AccountId = 1, Type = "deposit", Amount = 500m, CreatedAt = DateTime.UtcNow }
        };

        var accRepo = new FakeSavingsRepo(new[] { acc });
        var txRepo = new FakeTxRepo(seedTx);
        var svc = new TransactionService(txRepo, accRepo, new TestLogger<TransactionService>());

        var balance = await svc.GetBalanceAsync(1);

        Assert.Equal(1250m, balance);
    }

    [Fact]
    public async Task Execute_Deposit_CreatesPositiveLedgerEntryAndIncreasesBalance()
    {
        var acc = new SavingsAccount { Id = 1, CustomerId = 1, AccountNumber = "A1" };
        var accRepo = new FakeSavingsRepo(new[] { acc });
        var txRepo = new FakeTxRepo();
        var svc = new TransactionService(txRepo, accRepo, new TestLogger<TransactionService>());

        var req = new TransactionRequest(1, "deposit", 150m);
        var res = await svc.ExecuteAsync(req);

        Assert.Equal(1, res.AccountId);
        Assert.Equal(150m, res.Amount);
        Assert.Equal("deposit", res.Type);

        var balance = await svc.GetBalanceAsync(1);
        Assert.Equal(150m, balance);
    }

    [Fact]
    public async Task Execute_Withdrawal_WhenSufficientFunds_CreatesNegativeLedgerEntryAndDecreasesBalance()
    {
        var acc = new SavingsAccount { Id = 1, CustomerId = 1, AccountNumber = "A1" };
        var seedTx = new[]
        {
            new LedgerEntry { Id = 1, AccountId = 1, Type = "deposit", Amount = 500m, CreatedAt = DateTime.UtcNow }
        };

        var accRepo = new FakeSavingsRepo(new[] { acc });
        var txRepo = new FakeTxRepo(seedTx);
        var svc = new TransactionService(txRepo, accRepo, new TestLogger<TransactionService>());

        var req = new TransactionRequest(1, "withdrawal", 200m);
        var res = await svc.ExecuteAsync(req);

        Assert.Equal(1, res.AccountId);
        Assert.Equal(-200m, res.Amount);
        Assert.Equal("withdrawal", res.Type);

        var balance = await svc.GetBalanceAsync(1);
        Assert.Equal(300m, balance);
    }

    [Fact]
    public async Task Execute_Withdrawal_WhenInsufficientFunds_ThrowsAndCreatesNoEntry()
    {
        var acc = new SavingsAccount { Id = 1, CustomerId = 1, AccountNumber = "A1" };
        var seedTx = new[]
        {
            new LedgerEntry { Id = 1, AccountId = 1, Type = "deposit", Amount = 50m, CreatedAt = DateTime.UtcNow }
        };

        var accRepo = new FakeSavingsRepo(new[] { acc });
        var txRepo = new FakeTxRepo(seedTx);
        var svc = new TransactionService(txRepo, accRepo, new TestLogger<TransactionService>());

        var req = new TransactionRequest(1, "withdrawal", 100m);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ExecuteAsync(req));

        Assert.Equal(1, txRepo.Count); // No new entry created
        var balance = await svc.GetBalanceAsync(1);
        Assert.Equal(50m, balance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task Execute_InvalidAmount_ThrowsArgumentException(decimal invalidAmount)
    {
        var acc = new SavingsAccount { Id = 1, CustomerId = 1, AccountNumber = "A1" };
        var accRepo = new FakeSavingsRepo(new[] { acc });
        var txRepo = new FakeTxRepo();
        var svc = new TransactionService(txRepo, accRepo, new TestLogger<TransactionService>());

        var req = new TransactionRequest(1, "deposit", invalidAmount);

        await Assert.ThrowsAsync<ArgumentException>(() => svc.ExecuteAsync(req));
    }
}
