using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nordiska.BuildingBlocks.Database;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Contracts.Responses;
using Nordiska.Modules.Banking.Domain;
using Nordiska.Modules.Banking.Infrastructure;
using Xunit;

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
            var q = _store.Where(l => !l.IsPlanned).AsEnumerable();
            if (accountId.HasValue) q = q.Where(l => l.AccountId == accountId.Value);
            return Task.FromResult<IEnumerable<LedgerEntry>>(q.ToList());
        }

        public Task<PagedResult<LedgerEntry>> QueryPagedAsync(TransactionQueryParameters parameters, CancellationToken cancellationToken = default)
        {
            var q = _store.AsEnumerable();

            if (parameters.AccountIds != null && parameters.AccountIds.Count > 0)
            {
                q = q.Where(l => parameters.AccountIds.Contains(l.AccountId));
            }

            if (!string.IsNullOrWhiteSpace(parameters.Type))
            {
                var typeLower = parameters.Type.Trim().ToLowerInvariant();
                q = q.Where(l => l.Type.ToLowerInvariant() == typeLower);
            }

            if (parameters.FromDate.HasValue)
            {
                q = q.Where(l => l.CreatedAt >= parameters.FromDate.Value);
            }

            if (parameters.ToDate.HasValue)
            {
                q = q.Where(l => l.CreatedAt <= parameters.ToDate.Value);
            }

            if (parameters.MinAmount.HasValue)
            {
                q = q.Where(l => l.Amount >= parameters.MinAmount.Value);
            }

            if (parameters.MaxAmount.HasValue)
            {
                q = q.Where(l => l.Amount <= parameters.MaxAmount.Value);
            }

            if (!string.IsNullOrWhiteSpace(parameters.SearchTerm))
            {
                var term = parameters.SearchTerm.Trim().ToLowerInvariant();
                q = q.Where(l => l.Type.ToLowerInvariant().Contains(term) 
                                 || l.Id.ToString().Contains(term) 
                                 || l.AccountId.ToString().Contains(term));
            }

            var totalCount = q.Count();

            var isAsc = string.Equals(parameters.SortOrder, "asc", StringComparison.OrdinalIgnoreCase);
            var sortBy = parameters.SortBy?.Trim().ToLowerInvariant();

            q = sortBy switch
            {
                "amount" => isAsc ? q.OrderBy(l => l.Amount).ThenBy(l => l.Id) : q.OrderByDescending(l => l.Amount).ThenByDescending(l => l.Id),
                _ => isAsc ? q.OrderBy(l => l.CreatedAt).ThenBy(l => l.Id) : q.OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id)
            };

            var page = parameters.NormalizedPage;
            var pageSize = parameters.NormalizedPageSize;

            var items = q.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult(PagedResult<LedgerEntry>.Create(items, totalCount, page, pageSize));
        }

        public Task<LedgerEntry?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.FirstOrDefault(l => l.Id == id));

        public Task<long> CreateAsync(LedgerEntry entry, CancellationToken cancellationToken = default)
        {
            entry.Id = _next++;
            _store.Add(entry);
            return Task.FromResult(entry.Id);
        }

        public Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
        {
            var idx = _store.FindIndex(l => l.Id == id);
            if (idx >= 0)
            {
                _store.RemoveAt(idx);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
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
    public async Task QueryPaged_ReturnsPaginatedAndMetadata()
    {
        var baseDate = DateTime.UtcNow;
        var seed = Enumerable.Range(1, 25).Select(i => new LedgerEntry
        {
            Id = i,
            AccountId = 1,
            Type = i % 2 == 0 ? "deposit" : "withdrawal",
            Amount = i * 10,
            CreatedAt = baseDate.AddMinutes(i)
        }).ToList();

        var txRepo = new FakeTxRepo(seed);
        var accRepo = new FakeSavingsRepo();
        var svc = new TransactionService(txRepo, accRepo, new TestLogger<TransactionService>());

        var page1 = await svc.QueryPagedAsync(new TransactionQueryParameters(AccountIds: new[] { 1L }, Page: 1, PageSize: 10));
        Assert.Equal(25, page1.TotalCount);
        Assert.Equal(10, page1.Items.Count);
        Assert.Equal(1, page1.Page);
        Assert.Equal(3, page1.TotalPages);
        Assert.True(page1.HasNextPage);
        Assert.False(page1.HasPreviousPage);

        var page3 = await svc.QueryPagedAsync(new TransactionQueryParameters(AccountIds: new[] { 1L }, Page: 3, PageSize: 10));
        Assert.Equal(5, page3.Items.Count);
        Assert.Equal(3, page3.Page);
        Assert.False(page3.HasNextPage);
        Assert.True(page3.HasPreviousPage);
    }

    [Fact]
    public async Task QueryPaged_FiltersByTypeAndDateRange()
    {
        var baseDate = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        var seed = new[]
        {
            new LedgerEntry { Id = 1, AccountId = 1, Type = "deposit", Amount = 100, CreatedAt = baseDate.AddDays(1) },
            new LedgerEntry { Id = 2, AccountId = 1, Type = "withdrawal", Amount = -50, CreatedAt = baseDate.AddDays(2) },
            new LedgerEntry { Id = 3, AccountId = 1, Type = "deposit", Amount = 200, CreatedAt = baseDate.AddDays(5) },
            new LedgerEntry { Id = 4, AccountId = 1, Type = "deposit", Amount = 300, CreatedAt = baseDate.AddDays(10) }
        };

        var txRepo = new FakeTxRepo(seed);
        var accRepo = new FakeSavingsRepo();
        var svc = new TransactionService(txRepo, accRepo, new TestLogger<TransactionService>());

        var res = await svc.QueryPagedAsync(new TransactionQueryParameters(
            AccountIds: new[] { 1L },
            Type: "deposit",
            FromDate: baseDate.AddDays(2),
            ToDate: baseDate.AddDays(6)));

        Assert.Single(res.Items);
        Assert.Equal(3, res.Items.First().Id);
        Assert.Equal(200, res.Items.First().Amount);
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

        var req = new TransactionRequest(1, "deposit", 150m, Label: "Lön");
        var res = await svc.ExecuteAsync(req);

        Assert.Equal(1, res.AccountId);
        Assert.Equal(150m, res.Amount);
        Assert.Equal("deposit", res.Type);
        Assert.Equal("Lön", res.Label);

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

    [Fact]
    public async Task Transfer_TransfersFundsBetweenAccounts()
    {
        var acc1 = new SavingsAccount { Id = 1, CustomerId = 1, AccountNumber = "A1" };
        var acc2 = new SavingsAccount { Id = 2, CustomerId = 1, AccountNumber = "A2" };
        var seedTx = new[]
        {
            new LedgerEntry { Id = 1, AccountId = 1, Type = "deposit", Amount = 500m, CreatedAt = DateTime.UtcNow }
        };

        var accRepo = new FakeSavingsRepo(new[] { acc1, acc2 });
        var txRepo = new FakeTxRepo(seedTx);
        var svc = new TransactionService(txRepo, accRepo, new TestLogger<TransactionService>());

        var req = new TransferRequest(1, 2, 200m, "Överföring spar");
        var res = await svc.TransferAsync(req);

        Assert.Equal(1, res.AccountId);
        Assert.Equal(-200m, res.Amount);
        Assert.Equal("transfer", res.Type);
        Assert.Equal(2, res.TargetAccountId);
        Assert.Equal("Överföring spar", res.Label);

        Assert.Equal(300m, await svc.GetBalanceAsync(1));
        Assert.Equal(200m, await svc.GetBalanceAsync(2));
    }

    [Fact]
    public async Task Transfer_SameAccount_ThrowsArgumentException()
    {
        var acc1 = new SavingsAccount { Id = 1, CustomerId = 1, AccountNumber = "A1" };
        var accRepo = new FakeSavingsRepo(new[] { acc1 });
        var txRepo = new FakeTxRepo();
        var svc = new TransactionService(txRepo, accRepo, new TestLogger<TransactionService>());

        var req = new TransferRequest(1, 1, 100m);
        await Assert.ThrowsAsync<ArgumentException>(() => svc.TransferAsync(req));
    }

    [Fact]
    public async Task Transfer_InsufficientFunds_ThrowsInvalidOperationException()
    {
        var acc1 = new SavingsAccount { Id = 1, CustomerId = 1, AccountNumber = "A1" };
        var acc2 = new SavingsAccount { Id = 2, CustomerId = 1, AccountNumber = "A2" };
        var seedTx = new[]
        {
            new LedgerEntry { Id = 1, AccountId = 1, Type = "deposit", Amount = 50m, CreatedAt = DateTime.UtcNow }
        };

        var accRepo = new FakeSavingsRepo(new[] { acc1, acc2 });
        var txRepo = new FakeTxRepo(seedTx);
        var svc = new TransactionService(txRepo, accRepo, new TestLogger<TransactionService>());

        var req = new TransferRequest(1, 2, 100m);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.TransferAsync(req));
    }

    [Fact]
    public async Task CreatePlanned_CreatesPlannedEntry_DoesNotAffectBalance()
    {
        var acc = new SavingsAccount { Id = 1, CustomerId = 1, AccountNumber = "A1" };
        var accRepo = new FakeSavingsRepo(new[] { acc });
        var txRepo = new FakeTxRepo();
        var svc = new TransactionService(txRepo, accRepo, new TestLogger<TransactionService>());

        var plannedDate = DateTime.UtcNow.AddMonths(1);
        var req = new PlannedTransactionRequest(1, "withdrawal", 1200m, plannedDate, "Hyra");

        var res = await svc.CreatePlannedAsync(req);

        Assert.True(res.IsPlanned);
        Assert.Equal(plannedDate, res.PlannedDate);
        Assert.Equal("Hyra", res.Label);
        Assert.Equal(1, res.AccountId);

        var balance = await svc.GetBalanceAsync(1);
        Assert.Equal(0m, balance); // Planned entries do not change balance
    }

    [Fact]
    public async Task CancelPlanned_RemovesPlannedTransaction()
    {
        var acc = new SavingsAccount { Id = 1, CustomerId = 1, AccountNumber = "A1" };
        var seedTx = new[]
        {
            new LedgerEntry { Id = 10, AccountId = 1, Type = "withdrawal", Amount = -500m, IsPlanned = true, PlannedDate = DateTime.UtcNow.AddDays(5) }
        };

        var accRepo = new FakeSavingsRepo(new[] { acc });
        var txRepo = new FakeTxRepo(seedTx);
        var svc = new TransactionService(txRepo, accRepo, new TestLogger<TransactionService>());

        var deleted = await svc.CancelPlannedAsync(10);
        Assert.True(deleted);

        var fetched = await svc.GetByIdAsync(10);
        Assert.Null(fetched);
    }
}
