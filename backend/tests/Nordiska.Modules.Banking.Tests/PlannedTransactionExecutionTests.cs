using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nordiska.BuildingBlocks.Database;
using Nordiska.BuildingBlocks.Database.Errors;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Contracts.Responses;
using Nordiska.Modules.Banking.Domain;
using Nordiska.Modules.Banking.Infrastructure;
using Xunit;

namespace Nordiska.Modules.Banking.Tests;

public class PlannedTransactionExecutionTests
{
    private class InMemorySavingsRepo : ISavingsAccountRepository
    {
        public readonly List<SavingsAccount> Store = new();
        private long _next = 1;

        public InMemorySavingsRepo(IEnumerable<SavingsAccount>? seed = null)
        {
            if (seed != null)
            {
                Store.AddRange(seed);
                _next = (Store.MaxBy(s => s.Id)?.Id ?? 0) + 1;
            }
        }

        public Task<IEnumerable<SavingsAccount>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<SavingsAccount>>(Store.ToList());

        public Task<IEnumerable<SavingsAccount>> GetByCustomerIdAsync(long customerId, CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<SavingsAccount>>(Store.Where(s => s.CustomerId == customerId).ToList());

        public Task<SavingsAccount?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            => Task.FromResult(Store.FirstOrDefault(s => s.Id == id));

        public Task<long> CreateAsync(SavingsAccount entity, CancellationToken cancellationToken = default)
        {
            entity.Id = _next++;
            Store.Add(entity);
            return Task.FromResult(entity.Id);
        }

        public Task UpdateAsync(SavingsAccount entity, CancellationToken cancellationToken = default)
        {
            var idx = Store.FindIndex(s => s.Id == entity.Id);
            if (idx >= 0) Store[idx] = entity;
            return Task.CompletedTask;
        }
    }

    private class InMemoryTxRepo : ITransactionRepository
    {
        public readonly List<LedgerEntry> Store = new();
        private long _next = 1;

        public InMemoryTxRepo(IEnumerable<LedgerEntry>? seed = null)
        {
            if (seed != null)
            {
                Store.AddRange(seed);
                _next = (Store.MaxBy(t => t.Id)?.Id ?? 0) + 1;
            }
        }

        public Task<IEnumerable<LedgerEntry>> QueryAsync(long? accountId = null, CancellationToken cancellationToken = default)
        {
            var q = Store.Where(l => !l.IsPlanned).AsEnumerable();
            if (accountId.HasValue) q = q.Where(l => l.AccountId == accountId.Value);
            return Task.FromResult<IEnumerable<LedgerEntry>>(q.ToList());
        }

        public Task<PagedResult<LedgerEntry>> QueryPagedAsync(TransactionQueryParameters parameters, CancellationToken cancellationToken = default)
        {
            var q = Store.AsEnumerable();
            var totalCount = q.Count();
            var items = q.Skip((parameters.NormalizedPage - 1) * parameters.NormalizedPageSize).Take(parameters.NormalizedPageSize).ToList();
            return Task.FromResult(PagedResult<LedgerEntry>.Create(items, totalCount, parameters.NormalizedPage, parameters.NormalizedPageSize));
        }

        public Task<LedgerEntry?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            => Task.FromResult(Store.FirstOrDefault(l => l.Id == id));

        public Task<List<LedgerEntry>> GetPendingPlannedTransactionsAsync(DateTime asOfUtc, CancellationToken cancellationToken = default)
        {
            var pending = Store
                .Where(l => l.IsPlanned && l.PlannedDate.HasValue && l.PlannedDate.Value <= asOfUtc)
                .OrderBy(l => l.PlannedDate)
                .ThenBy(l => l.Id)
                .ToList();
            return Task.FromResult(pending);
        }

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
        {
            var idx = Store.FindIndex(l => l.Id == id);
            if (idx >= 0)
            {
                Store.RemoveAt(idx);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
    }

    [Fact]
    public async Task ProcessPlannedTransactionAsync_SingleTransfer_ExecutesAndDeletesPlan()
    {
        // Arrange
        var accRepo = new InMemorySavingsRepo(new[]
        {
            new SavingsAccount { Id = 1, CustomerId = 10, Balance = 1000m, AccountType = "Standard", CreatedAt = DateTime.UtcNow },
            new SavingsAccount { Id = 2, CustomerId = 10, Balance = 200m, AccountType = "Standard", CreatedAt = DateTime.UtcNow }
        });

        var txRepo = new InMemoryTxRepo(new[]
        {
            new LedgerEntry { Id = 1, AccountId = 1, Type = "deposit", Amount = 1000m, CreatedAt = DateTime.UtcNow.AddDays(-1) },
            new LedgerEntry { Id = 2, AccountId = 2, Type = "deposit", Amount = 200m, CreatedAt = DateTime.UtcNow.AddDays(-1) },
            new LedgerEntry
            {
                Id = 10,
                AccountId = 1,
                TargetAccountId = 2,
                Type = "transfer",
                Amount = 300m,
                Label = "Single transfer",
                IsPlanned = true,
                PlannedDate = DateTime.UtcNow.AddMinutes(-5),
                Repeating = null,
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            }
        });

        var service = new TransactionService(txRepo, accRepo, new TestLogger<TransactionService>());

        // Act
        var result = await service.ProcessPlannedTransactionAsync(10);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("transfer", result.Type);

        // Balances updated
        var balance1 = await service.GetBalanceAsync(1);
        var balance2 = await service.GetBalanceAsync(2);
        Assert.Equal(700m, balance1);
        Assert.Equal(500m, balance2);

        // Account models snapshot updated
        var acc1 = await accRepo.GetByIdAsync(1);
        var acc2 = await accRepo.GetByIdAsync(2);
        Assert.Equal(700m, acc1!.Balance);
        Assert.Equal(500m, acc2!.Balance);

        // Planned entry consumed and deleted
        var planAfter = await txRepo.GetByIdAsync(10);
        Assert.Null(planAfter);
    }

    [Theory]
    [InlineData("week")]
    [InlineData("month")]
    [InlineData("year")]
    public async Task ProcessPlannedTransactionAsync_RecurringTransfer_ExecutesAndAdvancesDate(string repeating)
    {
        // Arrange
        var accRepo = new InMemorySavingsRepo(new[]
        {
            new SavingsAccount { Id = 1, CustomerId = 10, Balance = 5000m, AccountType = "Standard", CreatedAt = DateTime.UtcNow },
            new SavingsAccount { Id = 2, CustomerId = 10, Balance = 100m, AccountType = "Standard", CreatedAt = DateTime.UtcNow }
        });

        var initialPlannedDate = new DateTime(2026, 9, 20, 10, 0, 0, DateTimeKind.Utc);
        var txRepo = new InMemoryTxRepo(new[]
        {
            new LedgerEntry { Id = 1, AccountId = 1, Type = "deposit", Amount = 5000m, CreatedAt = DateTime.UtcNow.AddDays(-5) },
            new LedgerEntry { Id = 2, AccountId = 2, Type = "deposit", Amount = 100m, CreatedAt = DateTime.UtcNow.AddDays(-5) },
            new LedgerEntry
            {
                Id = 15,
                AccountId = 1,
                TargetAccountId = 2,
                Type = "transfer",
                Amount = 500m,
                Label = "Recurring rent",
                IsPlanned = true,
                PlannedDate = initialPlannedDate,
                Repeating = repeating,
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            }
        });

        var service = new TransactionService(txRepo, accRepo, new TestLogger<TransactionService>());

        // Act
        var result = await service.ProcessPlannedTransactionAsync(15);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(4500m, await service.GetBalanceAsync(1));
        Assert.Equal(600m, await service.GetBalanceAsync(2));

        // Plan still exists and has advanced date
        var planAfter = await txRepo.GetByIdAsync(15);
        Assert.NotNull(planAfter);
        Assert.True(planAfter.IsPlanned);
        Assert.NotNull(planAfter.PlannedDate);
        Assert.True(planAfter.PlannedDate.Value > initialPlannedDate);

        if (repeating == "week")
        {
            Assert.Equal(initialPlannedDate.AddDays(7), planAfter.PlannedDate.Value);
        }
        else if (repeating == "month")
        {
            Assert.Equal(initialPlannedDate.AddMonths(1), planAfter.PlannedDate.Value);
        }
        else if (repeating == "year")
        {
            Assert.Equal(initialPlannedDate.AddYears(1), planAfter.PlannedDate.Value);
        }
    }

    [Fact]
    public async Task ProcessPlannedTransactionAsync_InsufficientFunds_ReturnsNullWithoutThrowing()
    {
        // Arrange
        var accRepo = new InMemorySavingsRepo(new[]
        {
            new SavingsAccount { Id = 1, CustomerId = 10, Balance = 50m, AccountType = "Standard", CreatedAt = DateTime.UtcNow },
            new SavingsAccount { Id = 2, CustomerId = 10, Balance = 100m, AccountType = "Standard", CreatedAt = DateTime.UtcNow }
        });

        var txRepo = new InMemoryTxRepo(new[]
        {
            new LedgerEntry { Id = 1, AccountId = 1, Type = "deposit", Amount = 50m, CreatedAt = DateTime.UtcNow.AddDays(-5) },
            new LedgerEntry
            {
                Id = 20,
                AccountId = 1,
                TargetAccountId = 2,
                Type = "transfer",
                Amount = 500m, // More than 50
                Label = "Overdraft attempt",
                IsPlanned = true,
                PlannedDate = DateTime.UtcNow.AddHours(-1),
                Repeating = "month",
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            }
        });

        var service = new TransactionService(txRepo, accRepo, new TestLogger<TransactionService>());

        // Act
        var result = await service.ProcessPlannedTransactionAsync(20);

        // Assert: gracefully returns null without blowing up
        Assert.Null(result);

        // Balance untouched
        Assert.Equal(50m, await service.GetBalanceAsync(1));

        // Planned transaction remains in store for retry or resolution
        var plan = await txRepo.GetByIdAsync(20);
        Assert.NotNull(plan);
    }

    [Fact]
    public async Task ProcessPlannedTransactionAsync_WhenTypeIsWithdrawWithTargetAccountId_ExecutesAsTransfer()
    {
        // Arrange
        var accRepo = new InMemorySavingsRepo(new[]
        {
            new SavingsAccount { Id = 1, CustomerId = 10, Balance = 1000m, AccountType = "Standard", CreatedAt = DateTime.UtcNow },
            new SavingsAccount { Id = 2, CustomerId = 10, Balance = 200m, AccountType = "Standard", CreatedAt = DateTime.UtcNow }
        });

        var txRepo = new InMemoryTxRepo(new[]
        {
            new LedgerEntry { Id = 1, AccountId = 1, Type = "deposit", Amount = 1000m, CreatedAt = DateTime.UtcNow.AddDays(-1) },
            new LedgerEntry { Id = 2, AccountId = 2, Type = "deposit", Amount = 200m, CreatedAt = DateTime.UtcNow.AddDays(-1) },
            new LedgerEntry
            {
                Id = 30,
                AccountId = 1,
                TargetAccountId = 2,
                Type = "withdraw", // Frontend sends "Withdraw"
                Amount = 250m,
                Label = "Transfer from frontend",
                IsPlanned = true,
                PlannedDate = DateTime.UtcNow.AddMinutes(-5),
                Repeating = null,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            }
        });

        var service = new TransactionService(txRepo, accRepo, new TestLogger<TransactionService>());

        // Act
        var result = await service.ProcessPlannedTransactionAsync(30);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("transfer", result.Type);
        Assert.Equal(750m, await service.GetBalanceAsync(1));
        Assert.Equal(450m, await service.GetBalanceAsync(2));
    }

    [Fact]
    public async Task ProcessPlannedTransactionAsync_NonExistentOrNotPlanned_ReturnsNull()
    {
        // Arrange
        var accRepo = new InMemorySavingsRepo();
        var txRepo = new InMemoryTxRepo(new[]
        {
            new LedgerEntry { Id = 1, AccountId = 1, Type = "deposit", Amount = 100m, IsPlanned = false, CreatedAt = DateTime.UtcNow }
        });

        var service = new TransactionService(txRepo, accRepo, new TestLogger<TransactionService>());

        // Act & Assert
        Assert.Null(await service.ProcessPlannedTransactionAsync(999)); // Not found
        Assert.Null(await service.ProcessPlannedTransactionAsync(1));   // Not planned
    }
}
