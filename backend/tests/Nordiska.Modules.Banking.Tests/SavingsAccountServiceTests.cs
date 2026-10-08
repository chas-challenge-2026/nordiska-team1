using System.Threading;
using Nordiska.BuildingBlocks.Database;
using Nordiska.BuildingBlocks.Database.Errors;
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

        public Task<IEnumerable<SavingsAccount>> GetByCustomerIdAsync(long customerId, CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<SavingsAccount>>(_store.Where(s => s.CustomerId == customerId).ToList());

        public Task<SavingsAccount?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.FirstOrDefault(s => s.Id == id));

        public Task<int> CountFavoritesByCustomerIdAsync(long customerId, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.Count(s => s.CustomerId == customerId && s.IsFavorite && s.Status == "active"));

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
    public async Task GetAll_ReturnsAllAccounts()
    {
        var seed = new[]
        {
            new SavingsAccount { Id = 1, CustomerId = 1, AccountNumber = "A1", AccountType = "standard", Balance = 100 },
            new SavingsAccount { Id = 2, CustomerId = 2, AccountNumber = "A2", AccountType = "premium", Balance = 200 }
        };

        var repo = new FakeSavingsRepo(seed);
        var service = new SavingsAccountService(repo, new TestLogger<SavingsAccountService>());

        var all = await service.GetAllAsync();

        Assert.Equal(2, all.Count());
    }

    [Fact]
    public async Task GetByCustomerId_ReturnsOnlyThatCustomersAccounts()
    {
        var seed = new[]
        {
            new SavingsAccount { Id = 1, CustomerId = 1, AccountNumber = "A1", AccountType = "Standard", Balance = 100 },
            new SavingsAccount { Id = 2, CustomerId = 2, AccountNumber = "A2", AccountType = "Premium", Balance = 200 },
            new SavingsAccount { Id = 3, CustomerId = 1, AccountNumber = "A3", AccountType = "Standard", Balance = 300 }
        };

        var repo = new FakeSavingsRepo(seed);
        var service = new SavingsAccountService(repo, new TestLogger<SavingsAccountService>());

        var res = await service.GetByCustomerIdAsync(1);

        Assert.Equal(new long[] { 1, 3 }, res.Select(a => a.Id).OrderBy(id => id));
    }

    [Fact]
    public async Task Create_AddsNewAccount_ReturnsResponse()
    {
        var repo = new FakeSavingsRepo();
        var service = new SavingsAccountService(repo, new TestLogger<SavingsAccountService>());

        var req = new OpenSavingsAccountRequest(1, "SE1234", "standard", 500m, 0.025m);

        var created = await service.CreateAsync(req);

        Assert.NotNull(created);
        Assert.True(created.Id > 0);
        Assert.Equal(req.CustomerId, created.CustomerId);
        Assert.Equal(req.AccountNumber, created.AccountNumber);
        Assert.Equal(req.InitialDeposit, created.Balance);
        Assert.Equal("standard", created.AccountType);
        Assert.Equal(0.025m, created.InterestRate);
        Assert.Equal("SEK", created.CurrencyCode);
    }

    [Fact]
    public async Task CloseAccount_WithZeroBalance_SetsStatusClosed()
    {
        var seed = new[]
        {
            new SavingsAccount { Id = 1, CustomerId = 1, AccountNumber = "A1", AccountType = "standard", Balance = 0, Status = "active" }
        };

        var repo = new FakeSavingsRepo(seed);
        var service = new SavingsAccountService(repo, new TestLogger<SavingsAccountService>());

        var closed = await service.CloseAccountAsync(1);

        Assert.Equal("closed", closed.Status);
        Assert.NotNull(closed.UpdatedAt);
    }

    [Fact]
    public async Task CloseAccount_WithPositiveBalance_ThrowsConflictException()
    {
        var seed = new[]
        {
            new SavingsAccount { Id = 1, CustomerId = 1, AccountNumber = "A1", AccountType = "standard", Balance = 500m, Status = "active" }
        };

        var repo = new FakeSavingsRepo(seed);
        var service = new SavingsAccountService(repo, new TestLogger<SavingsAccountService>());

        await Assert.ThrowsAsync<ConflictException>(() => service.CloseAccountAsync(1));
    }

    [Fact]
    public async Task GetById_NotFound_ThrowsNotFoundException()
    {
        var repo = new FakeSavingsRepo();
        var service = new SavingsAccountService(repo, new TestLogger<SavingsAccountService>());

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(42));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_WithoutAccountNumber_GeneratesNorNumber(string accountNumber)
    {
        var repo = new FakeSavingsRepo();
        var service = new SavingsAccountService(repo, new TestLogger<SavingsAccountService>());

        var created = await service.CreateAsync(new OpenSavingsAccountRequest(1, accountNumber));

        Assert.Matches(@"^NOR-\d{6}$", created.AccountNumber);
    }

    [Fact]
    public async Task Create_WithAccountNumber_TrimsWhitespace()
    {
        var repo = new FakeSavingsRepo();
        var service = new SavingsAccountService(repo, new TestLogger<SavingsAccountService>());

        var created = await service.CreateAsync(new OpenSavingsAccountRequest(1, "  SE1234  "));

        Assert.Equal("SE1234", created.AccountNumber);
    }

    [Fact]
    public async Task Create_WithUnsupportedAccountType_ThrowsNotFoundException()
    {
        var repo = new FakeSavingsRepo();
        var service = new SavingsAccountService(repo, new TestLogger<SavingsAccountService>());

        var req = new OpenSavingsAccountRequest(1, "NOR-999999", "unsupported_crypto_type");

        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateAsync(req));
    }

    private class FakeTxRepo : ITransactionRepository
    {
        public readonly List<LedgerEntry> Store = new();
        private long _next = 1;

        public Task<IEnumerable<LedgerEntry>> QueryAsync(long? accountId = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<LedgerEntry>>(accountId.HasValue ? Store.Where(e => e.AccountId == accountId.Value).ToList() : Store.ToList());

        public Task<PagedResult<LedgerEntry>> QueryPagedAsync(TransactionQueryParameters parameters, CancellationToken cancellationToken = default)
            => Task.FromResult(PagedResult<LedgerEntry>.Create(Store, Store.Count, 1, 10));

        public Task<LedgerEntry?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            => Task.FromResult(Store.FirstOrDefault(e => e.Id == id));

        public Task<List<LedgerEntry>> GetPendingPlannedTransactionsAsync(DateTime asOfUtc, CancellationToken cancellationToken = default)
            => Task.FromResult(Store.Where(l => l.IsPlanned && l.PlannedDate.HasValue && l.PlannedDate.Value <= asOfUtc).ToList());

        public Task<long> CreateAsync(LedgerEntry entry, CancellationToken cancellationToken = default)
        {
            entry.Id = _next++;
            Store.Add(entry);
            return Task.FromResult(entry.Id);
        }

        public Task<bool> UpdateAsync(LedgerEntry entry, CancellationToken cancellationToken = default)
        {
            var idx = Store.FindIndex(l => l.Id == entry.Id);
            if (idx >= 0)
            {
                Store[idx] = entry;
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
            => Task.FromResult(Store.RemoveAll(e => e.Id == id) > 0);
    }

    [Fact]
    public async Task Create_WithInitialDeposit_CreatesLedgerDepositEntry()
    {
        var savingsRepo = new FakeSavingsRepo();
        var txRepo = new FakeTxRepo();
        var service = new SavingsAccountService(savingsRepo, new TestLogger<SavingsAccountService>(), null, txRepo);

        var req = new OpenSavingsAccountRequest(1, "NOR-123456", "standard", 750m, 0.025m);

        var created = await service.CreateAsync(req);

        Assert.NotNull(created);
        Assert.Equal(750m, created.Balance);
        Assert.Single(txRepo.Store);
        var entry = txRepo.Store.First();
        Assert.Equal(created.Id, entry.AccountId);
        Assert.Equal("deposit", entry.Type);
        Assert.Equal(750m, entry.Amount);
        Assert.True(created.EstimatedYearEndInterest > 0m);
    }

    [Fact]
    public async Task GetById_AccountWithBalanceButNoLedgerEntries_CalculatesInterestUsingFallback()
    {
        var acc = new SavingsAccount
        {
            Id = 42,
            CustomerId = 1,
            AccountNumber = "NOR-999888",
            AccountType = "saving",
            Balance = 10000m,
            InterestRate = 0.035m,
            Status = "active",
            CreatedAt = new DateTime(DateTime.UtcNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        var savingsRepo = new FakeSavingsRepo(new[] { acc });
        var txRepo = new FakeTxRepo(); // empty tx repo
        var service = new SavingsAccountService(savingsRepo, new TestLogger<SavingsAccountService>(), null, txRepo);

        var result = await service.GetByIdAsync(42);

        Assert.NotNull(result);
        Assert.Equal(10000m, result.Balance);
        Assert.True(result.EstimatedYearEndInterest > 0m);
        Assert.True(result.AccruedInterestYtd > 0m);
    }

    [Fact]
    public async Task SetFavorite_WhenUnderLimit_UpdatesAccountToFavorite()
    {
        var seed = new[]
        {
            new SavingsAccount { Id = 1, CustomerId = 1, AccountNumber = "A1", AccountType = "standard", IsFavorite = false, Status = "active" }
        };

        var repo = new FakeSavingsRepo(seed);
        var service = new SavingsAccountService(repo, new TestLogger<SavingsAccountService>());

        var result = await service.SetFavoriteAsync(1, 1, true);

        Assert.True(result.IsFavorite);
        var inRepo = await repo.GetByIdAsync(1);
        Assert.True(inRepo?.IsFavorite);
    }

    [Fact]
    public async Task SetFavorite_WhenReachingFiveFavorites_ThrowsValidationException()
    {
        var seed = new[]
        {
            new SavingsAccount { Id = 1, CustomerId = 1, AccountNumber = "A1", IsFavorite = true, Status = "active" },
            new SavingsAccount { Id = 2, CustomerId = 1, AccountNumber = "A2", IsFavorite = true, Status = "active" },
            new SavingsAccount { Id = 3, CustomerId = 1, AccountNumber = "A3", IsFavorite = true, Status = "active" },
            new SavingsAccount { Id = 4, CustomerId = 1, AccountNumber = "A4", IsFavorite = true, Status = "active" },
            new SavingsAccount { Id = 5, CustomerId = 1, AccountNumber = "A5", IsFavorite = false, Status = "active" }
        };

        var repo = new FakeSavingsRepo(seed);
        var service = new SavingsAccountService(repo, new TestLogger<SavingsAccountService>());

        var ex = await Assert.ThrowsAsync<ValidationException>(() => service.SetFavoriteAsync(5, 1, true));
        Assert.Equal("Du kan maximalt ha 4 favoritkonton markerade samtidigt.", ex.Message);
    }

    [Fact]
    public async Task SetFavorite_UnsettingFavorite_SucceedsEvenIfLimitWasReached()
    {
        var seed = new[]
        {
            new SavingsAccount { Id = 1, CustomerId = 1, AccountNumber = "A1", IsFavorite = true, Status = "active" }
        };

        var repo = new FakeSavingsRepo(seed);
        var service = new SavingsAccountService(repo, new TestLogger<SavingsAccountService>());

        var result = await service.SetFavoriteAsync(1, 1, false);

        Assert.False(result.IsFavorite);
    }

    [Fact]
    public async Task GetByCustomerId_WithIsFavoriteTrue_ReturnsOnlyFavorites()
    {
        var seed = new[]
        {
            new SavingsAccount { Id = 1, CustomerId = 1, AccountNumber = "A1", IsFavorite = true, Status = "active" },
            new SavingsAccount { Id = 2, CustomerId = 1, AccountNumber = "A2", IsFavorite = false, Status = "active" },
            new SavingsAccount { Id = 3, CustomerId = 1, AccountNumber = "A3", IsFavorite = true, Status = "active" }
        };

        var repo = new FakeSavingsRepo(seed);
        var service = new SavingsAccountService(repo, new TestLogger<SavingsAccountService>());

        var favorites = await service.GetByCustomerIdAsync(1, isFavorite: true);

        Assert.Equal(2, favorites.Count());
        Assert.All(favorites, f => Assert.True(f.IsFavorite));
    }
}
