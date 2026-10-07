using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nordiska.BuildingBlocks.Database;
using Nordiska.BuildingBlocks.Database.Errors;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Domain;
using Nordiska.Modules.Banking.Infrastructure;
using Xunit;

namespace Nordiska.Modules.Banking.Tests;

public class SavingsGoalAutomationTests
{
    private class InMemoryGoalRepo : ISavingsGoalRepository
    {
        public readonly List<SavingsGoal> Store = new();
        private long _next = 1;

        public Task<SavingsGoal?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            => Task.FromResult(Store.FirstOrDefault(g => g.Id == id));

        public Task<List<SavingsGoal>> GetByCustomerIdAsync(long customerId, CancellationToken cancellationToken = default)
            => Task.FromResult(Store.Where(g => g.CustomerId == customerId).ToList());

        public Task<List<SavingsGoal>> GetByAccountIdAsync(long accountId, CancellationToken cancellationToken = default)
            => Task.FromResult(Store.Where(g => g.AccountId == accountId).ToList());

        public Task<long> CreateAsync(SavingsGoal goal, CancellationToken cancellationToken = default)
        {
            goal.Id = _next++;
            Store.Add(goal);
            return Task.FromResult(goal.Id);
        }

        public Task<bool> UpdateAsync(SavingsGoal goal, CancellationToken cancellationToken = default)
        {
            var idx = Store.FindIndex(g => g.Id == goal.Id);
            if (idx >= 0)
            {
                Store[idx] = goal;
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
        {
            var count = Store.RemoveAll(g => g.Id == id);
            return Task.FromResult(count > 0);
        }
    }

    private class InMemorySavingsAccountRepo : ISavingsAccountRepository
    {
        public readonly List<SavingsAccount> Store = new();
        private long _next = 1;

        public InMemorySavingsAccountRepo(IEnumerable<SavingsAccount>? seed = null)
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

        public Task<int> CountFavoritesByCustomerIdAsync(long customerId, CancellationToken cancellationToken = default)
            => Task.FromResult(Store.Count(s => s.CustomerId == customerId && s.IsFavorite));

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
                _next = (Store.MaxBy(e => e.Id)?.Id ?? 0) + 1;
            }
        }

        public Task<IEnumerable<LedgerEntry>> QueryAsync(long? accountId = null, CancellationToken cancellationToken = default)
        {
            var q = Store.Where(l => !l.IsPlanned).AsEnumerable();
            if (accountId.HasValue) q = q.Where(l => l.AccountId == accountId.Value);
            return Task.FromResult<IEnumerable<LedgerEntry>>(q.ToList());
        }

        public Task<PagedResult<LedgerEntry>> QueryPagedAsync(TransactionQueryParameters parameters, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<LedgerEntry?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            => Task.FromResult(Store.FirstOrDefault(e => e.Id == id));

        public Task<List<LedgerEntry>> GetPendingPlannedTransactionsAsync(DateTime asOfUtc, CancellationToken cancellationToken = default)
            => Task.FromResult(Store.Where(e => e.IsPlanned && e.PlannedDate <= asOfUtc).ToList());

        public Task<long> CreateAsync(LedgerEntry entry, CancellationToken cancellationToken = default)
        {
            entry.Id = _next++;
            Store.Add(entry);
            return Task.FromResult(entry.Id);
        }

        public Task<bool> UpdateAsync(LedgerEntry entry, CancellationToken cancellationToken = default)
        {
            var idx = Store.FindIndex(e => e.Id == entry.Id);
            if (idx >= 0)
            {
                Store[idx] = entry;
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
        {
            var count = Store.RemoveAll(e => e.Id == id);
            return Task.FromResult(count > 0);
        }

        public Task<LedgerEntry?> GetPlannedTransactionByGoalIdAsync(long savingsGoalId, CancellationToken cancellationToken = default)
            => Task.FromResult(Store.FirstOrDefault(e => e.SavingsGoalId == savingsGoalId && e.IsPlanned));
    }

    [Fact]
    public async Task AutomateAsync_CreatesRecurringPlannedTransaction_WhenValid()
    {
        var goalRepo = new InMemoryGoalRepo();
        var accountRepo = new InMemorySavingsAccountRepo();
        var txRepo = new InMemoryTxRepo();
        var service = new SavingsGoalService(goalRepo, accountRepo, txRepo, NullLogger<SavingsGoalService>.Instance);

        var sourceAcc = new SavingsAccount { Id = 10, CustomerId = 1, Balance = 5000, Status = "active" };
        var targetAcc = new SavingsAccount { Id = 20, CustomerId = 1, Balance = 1000, Status = "active" };
        accountRepo.Store.Add(sourceAcc);
        accountRepo.Store.Add(targetAcc);

        var goal = new SavingsGoal
        {
            Id = 100,
            AccountId = 20,
            CustomerId = 1,
            Title = "Japanresa",
            TargetAmount = 25000,
            CurrentAmount = 0,
            Status = "active"
        };
        goalRepo.Store.Add(goal);

        var request = new AutomateSavingsGoalRequest(
            SourceAccountId: 10,
            MonthlyAmount: 1500m,
            DayOfMonth: 25
        );

        var result = await service.AutomateAsync(100, request, customerId: 1);

        Assert.Equal(100, result.SavingsGoalId);
        Assert.Equal(10, result.SourceAccountId);
        Assert.Equal(20, result.TargetAccountId);
        Assert.Equal(1500m, result.MonthlyAmount);
        Assert.Equal(25, result.DayOfMonth);
        Assert.Equal("active", result.Status);

        Assert.Single(txRepo.Store);
        var plan = txRepo.Store[0];
        Assert.True(plan.IsPlanned);
        Assert.Equal("month", plan.Repeating);
        Assert.Equal(100, plan.SavingsGoalId);
        Assert.Equal(10, plan.AccountId);
        Assert.Equal(20, plan.TargetAccountId);
        Assert.Equal(1500m, plan.Amount);
    }

    [Fact]
    public async Task AutomateAsync_UpdatesExistingPlan_WhenAlreadyScheduled()
    {
        var goalRepo = new InMemoryGoalRepo();
        var accountRepo = new InMemorySavingsAccountRepo();
        var txRepo = new InMemoryTxRepo();
        var service = new SavingsGoalService(goalRepo, accountRepo, txRepo, NullLogger<SavingsGoalService>.Instance);

        accountRepo.Store.Add(new SavingsAccount { Id = 10, CustomerId = 1, Balance = 5000, Status = "active" });
        accountRepo.Store.Add(new SavingsAccount { Id = 11, CustomerId = 1, Balance = 3000, Status = "active" });
        accountRepo.Store.Add(new SavingsAccount { Id = 20, CustomerId = 1, Balance = 1000, Status = "active" });

        var goal = new SavingsGoal { Id = 100, AccountId = 20, CustomerId = 1, Title = "Buffert", TargetAmount = 10000, Status = "active" };
        goalRepo.Store.Add(goal);

        txRepo.Store.Add(new LedgerEntry
        {
            Id = 1,
            AccountId = 10,
            TargetAccountId = 20,
            Amount = 500m,
            IsPlanned = true,
            Repeating = "month",
            SavingsGoalId = 100
        });

        var request = new AutomateSavingsGoalRequest(
            SourceAccountId: 11,
            MonthlyAmount: 2000m,
            DayOfMonth: 15
        );

        var result = await service.AutomateAsync(100, request, customerId: 1);

        Assert.Equal(2000m, result.MonthlyAmount);
        Assert.Equal(15, result.DayOfMonth);
        Assert.Single(txRepo.Store);
        var plan = txRepo.Store[0];
        Assert.Equal(11, plan.AccountId);
        Assert.Equal(2000m, plan.Amount);
    }

    [Fact]
    public async Task AutomateAsync_ThrowsValidation_WhenAmountOrDayInvalid()
    {
        var goalRepo = new InMemoryGoalRepo();
        var accountRepo = new InMemorySavingsAccountRepo();
        var txRepo = new InMemoryTxRepo();
        var service = new SavingsGoalService(goalRepo, accountRepo, txRepo, NullLogger<SavingsGoalService>.Instance);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.AutomateAsync(1, new AutomateSavingsGoalRequest(10, 0m, 25), 1));

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.AutomateAsync(1, new AutomateSavingsGoalRequest(10, 500m, 0), 1));

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.AutomateAsync(1, new AutomateSavingsGoalRequest(10, 500m, 32), 1));
    }

    [Fact]
    public async Task AutomateAsync_ThrowsValidation_WhenSourceAccountIsSameAsTarget()
    {
        var goalRepo = new InMemoryGoalRepo();
        var accountRepo = new InMemorySavingsAccountRepo();
        var txRepo = new InMemoryTxRepo();
        var service = new SavingsGoalService(goalRepo, accountRepo, txRepo, NullLogger<SavingsGoalService>.Instance);

        accountRepo.Store.Add(new SavingsAccount { Id = 20, CustomerId = 1, Balance = 5000, Status = "active" });
        goalRepo.Store.Add(new SavingsGoal { Id = 100, AccountId = 20, CustomerId = 1, Title = "Mål", TargetAmount = 5000 });

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.AutomateAsync(100, new AutomateSavingsGoalRequest(20, 500m, 25), 1));
    }

    [Fact]
    public async Task CancelAutomationAsync_RemovesPlannedTransaction()
    {
        var goalRepo = new InMemoryGoalRepo();
        var accountRepo = new InMemorySavingsAccountRepo();
        var txRepo = new InMemoryTxRepo();
        var service = new SavingsGoalService(goalRepo, accountRepo, txRepo, NullLogger<SavingsGoalService>.Instance);

        goalRepo.Store.Add(new SavingsGoal { Id = 100, CustomerId = 1, AccountId = 20, Title = "Test" });
        txRepo.Store.Add(new LedgerEntry { Id = 5, SavingsGoalId = 100, IsPlanned = true });

        var success = await service.CancelAutomationAsync(100, customerId: 1);

        Assert.True(success);
        Assert.Empty(txRepo.Store);
    }

    [Fact]
    public void CalculateNextExecutionDate_ClampsDayAndSchedulesFutureDate()
    {
        // 1. Current day is 10th, scheduled for 25th -> Should be 25th this month
        var date1 = new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc);
        var next1 = SavingsGoalService.CalculateNextExecutionDate(25, date1);
        Assert.Equal(new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc), next1);

        // 2. Current day is 26th, scheduled for 25th -> Should be 25th next month
        var date2 = new DateTime(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc);
        var next2 = SavingsGoalService.CalculateNextExecutionDate(25, date2);
        Assert.Equal(new DateTime(2026, 6, 25, 0, 0, 0, DateTimeKind.Utc), next2);

        // 3. Clamping for 31st when next month has 30 days (June)
        var date3 = new DateTime(2026, 5, 31, 23, 0, 0, DateTimeKind.Utc);
        var next3 = SavingsGoalService.CalculateNextExecutionDate(31, date3);
        Assert.Equal(new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc), next3);
    }

    [Fact]
    public async Task ProcessPlannedTransaction_SkipsAndAdvances_WhenGoalIsPaused()
    {
        var accRepo = new InMemorySavingsAccountRepo(new[]
        {
            new SavingsAccount { Id = 1, CustomerId = 1, Balance = 2000, Status = "active" },
            new SavingsAccount { Id = 2, CustomerId = 1, Balance = 500, Status = "active" }
        });

        var goal = new SavingsGoal
        {
            Id = 50,
            AccountId = 2,
            CustomerId = 1,
            Title = "Pausat Mål",
            TargetAmount = 10000,
            CurrentAmount = 500,
            Status = "paused"
        };
        var goalRepo = new InMemoryGoalRepo();
        goalRepo.Store.Add(goal);

        var initialDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var plan = new LedgerEntry
        {
            Id = 99,
            AccountId = 1,
            TargetAccountId = 2,
            Amount = 300,
            Type = "transfer",
            IsPlanned = true,
            Repeating = "month",
            PlannedDate = initialDate,
            SavingsGoalId = 50
        };

        var txRepo = new InMemoryTxRepo(new[]
        {
            new LedgerEntry { Id = 1, AccountId = 1, Type = "deposit", Amount = 2000, CreatedAt = DateTime.UtcNow.AddDays(-1) },
            new LedgerEntry { Id = 2, AccountId = 2, Type = "deposit", Amount = 500, CreatedAt = DateTime.UtcNow.AddDays(-1) },
            plan
        });

        var txService = new TransactionService(txRepo, accRepo, NullLogger<TransactionService>.Instance, goalRepo);

        var response = await txService.ProcessPlannedTransactionAsync(99);

        // Skipped: No transaction returned
        Assert.Null(response);

        // Balances did NOT change
        Assert.Equal(2000, accRepo.Store[0].Balance);
        Assert.Equal(500, accRepo.Store[1].Balance);

        // Goal CurrentAmount did NOT change
        Assert.Equal(500, goal.CurrentAmount);

        // PlannedDate advanced by 1 month
        Assert.Equal(initialDate.AddMonths(1), plan.PlannedDate);
    }

    [Fact]
    public async Task ProcessPlannedTransaction_ExecutesAndCompletesGoal_WhenActiveAndTargetReached()
    {
        var accRepo = new InMemorySavingsAccountRepo(new[]
        {
            new SavingsAccount { Id = 1, CustomerId = 1, Balance = 2000, Status = "active" },
            new SavingsAccount { Id = 2, CustomerId = 1, Balance = 500, Status = "active" }
        });

        var goal = new SavingsGoal
        {
            Id = 50,
            AccountId = 2,
            CustomerId = 1,
            Title = "Ny Dator",
            TargetAmount = 1000,
            CurrentAmount = 700,
            Status = "active"
        };
        var goalRepo = new InMemoryGoalRepo();
        goalRepo.Store.Add(goal);

        var initialDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var plan = new LedgerEntry
        {
            Id = 99,
            AccountId = 1,
            TargetAccountId = 2,
            Amount = 300,
            Type = "transfer",
            IsPlanned = true,
            Repeating = "month",
            PlannedDate = initialDate,
            SavingsGoalId = 50
        };

        var txRepo = new InMemoryTxRepo(new[]
        {
            new LedgerEntry { Id = 1, AccountId = 1, Type = "deposit", Amount = 2000, CreatedAt = DateTime.UtcNow.AddDays(-1) },
            new LedgerEntry { Id = 2, AccountId = 2, Type = "deposit", Amount = 500, CreatedAt = DateTime.UtcNow.AddDays(-1) },
            plan
        });

        var txService = new TransactionService(txRepo, accRepo, NullLogger<TransactionService>.Instance, goalRepo);

        var response = await txService.ProcessPlannedTransactionAsync(99);

        // Successfully executed
        Assert.NotNull(response);

        // Balances updated
        Assert.Equal(1700, accRepo.Store[0].Balance);
        Assert.Equal(800, accRepo.Store[1].Balance);

        // Goal CurrentAmount incremented to 1000
        Assert.Equal(1000, goal.CurrentAmount);

        // Reached target amount -> Status changed to completed!
        Assert.Equal("completed", goal.Status);

        // Next execution date advanced
        Assert.Equal(initialDate.AddMonths(1), plan.PlannedDate);
    }
}
