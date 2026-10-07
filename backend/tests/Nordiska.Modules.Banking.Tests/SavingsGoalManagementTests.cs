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

public class SavingsGoalManagementTests
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

    private readonly InMemoryGoalRepo _goalRepo = new();
    private readonly InMemorySavingsAccountRepo _accountRepo = new();
    private readonly InMemoryTxRepo _txRepo = new();
    private readonly SavingsGoalService _service;

    public SavingsGoalManagementTests()
    {
        _service = new SavingsGoalService(
            _goalRepo,
            _accountRepo,
            _txRepo,
            NullLogger<SavingsGoalService>.Instance);

        _accountRepo.Store.Add(new SavingsAccount
        {
            Id = 10,
            CustomerId = 1,
            AccountNumber = "ACC-10",
            AccountType = "flex",
            Balance = 5000m,
            Status = "active"
        });

        _accountRepo.Store.Add(new SavingsAccount
        {
            Id = 20,
            CustomerId = 2,
            AccountNumber = "ACC-20",
            AccountType = "sparkonto",
            Balance = 10000m,
            Status = "active"
        });
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesGoalWithActiveStatusAndZeroCurrentAmount()
    {
        var targetDate = DateTime.UtcNow.AddMonths(6);
        var request = new CreateSavingsGoalRequest(
            AccountId: 10,
            Title: "Semesterresa 2027",
            TargetAmount: 20000m,
            TargetDate: targetDate);

        var result = await _service.CreateAsync(request, customerId: 1);

        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal("Semesterresa 2027", result.Title);
        Assert.Equal(20000m, result.TargetAmount);
        Assert.Equal(0m, result.CurrentAmount);
        Assert.Equal("active", result.Status);
        Assert.Equal(0m, result.ProgressPercentage);
        Assert.Equal(1, result.CustomerId);
        Assert.Equal(10, result.AccountId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Detta är ett extremt långt namn på ett sparmål som överstiger gränsen på femtio tecken och ska därför valideras bort")]
    public async Task CreateAsync_InvalidTitle_ThrowsValidationException(string title)
    {
        var request = new CreateSavingsGoalRequest(10, title, 5000m, DateTime.UtcNow.AddMonths(1));

        await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(request, customerId: 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-500)]
    public async Task CreateAsync_InvalidTargetAmount_ThrowsValidationException(decimal amount)
    {
        var request = new CreateSavingsGoalRequest(10, "Buffert", amount, DateTime.UtcNow.AddMonths(1));

        await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(request, customerId: 1));
    }

    [Fact]
    public async Task CreateAsync_TargetDateInPast_ThrowsValidationException()
    {
        var request = new CreateSavingsGoalRequest(10, "Buffert", 5000m, DateTime.UtcNow.AddDays(-1));

        await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(request, customerId: 1));
    }

    [Fact]
    public async Task CreateAsync_AccountNotFound_ThrowsNotFoundException()
    {
        var request = new CreateSavingsGoalRequest(999, "Buffert", 5000m, DateTime.UtcNow.AddMonths(1));

        await Assert.ThrowsAsync<NotFoundException>(() => _service.CreateAsync(request, customerId: 1));
    }

    [Fact]
    public async Task CreateAsync_NotOwnerOfAccount_ThrowsValidationException()
    {
        // Account 20 belongs to customer 2, but caller is customer 1
        var request = new CreateSavingsGoalRequest(20, "Hoppsan", 5000m, DateTime.UtcNow.AddMonths(1));

        await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(request, customerId: 1));
    }

    [Fact]
    public async Task GetGoalsAsync_ReturnsOnlyGoalsForCallingCustomer()
    {
        _goalRepo.Store.Add(new SavingsGoal { Id = 1, CustomerId = 1, AccountId = 10, Title = "Mål 1", TargetAmount = 5000m, Status = "active" });
        _goalRepo.Store.Add(new SavingsGoal { Id = 2, CustomerId = 1, AccountId = 10, Title = "Mål 2", TargetAmount = 8000m, Status = "active" });
        _goalRepo.Store.Add(new SavingsGoal { Id = 3, CustomerId = 2, AccountId = 20, Title = "Annan kunds mål", TargetAmount = 10000m, Status = "active" });

        var goals = await _service.GetGoalsAsync(customerId: 1);

        Assert.Equal(2, goals.Count);
        Assert.All(goals, g => Assert.Equal(1, g.CustomerId));
    }

    [Fact]
    public async Task GetGoalsAsync_WithAccountIdFilter_ReturnsGoalsForThatAccount()
    {
        _accountRepo.Store.Add(new SavingsAccount { Id = 11, CustomerId = 1, AccountNumber = "ACC-11", Status = "active" });
        _goalRepo.Store.Add(new SavingsGoal { Id = 1, CustomerId = 1, AccountId = 10, Title = "Mål på konto 10", TargetAmount = 5000m, Status = "active" });
        _goalRepo.Store.Add(new SavingsGoal { Id = 2, CustomerId = 1, AccountId = 11, Title = "Mål på konto 11", TargetAmount = 8000m, Status = "active" });

        var goals = await _service.GetGoalsAsync(customerId: 1, accountId: 10);

        Assert.Single(goals);
        Assert.Equal("Mål på konto 10", goals[0].Title);
    }

    [Fact]
    public async Task GetByIdAsync_WhenGoalBelongsToCustomer_ReturnsGoal()
    {
        _goalRepo.Store.Add(new SavingsGoal { Id = 1, CustomerId = 1, AccountId = 10, Title = "Mål 1", TargetAmount = 10000m, CurrentAmount = 2500m, Status = "active" });

        var goal = await _service.GetByIdAsync(1, customerId: 1);

        Assert.NotNull(goal);
        Assert.Equal("Mål 1", goal.Title);
        Assert.Equal(25.0m, goal.ProgressPercentage);
    }

    [Fact]
    public async Task GetByIdAsync_WhenGoalBelongsToOtherCustomer_ReturnsNull()
    {
        _goalRepo.Store.Add(new SavingsGoal { Id = 1, CustomerId = 2, AccountId = 20, Title = "Mål 2", TargetAmount = 10000m, Status = "active" });

        var goal = await _service.GetByIdAsync(1, customerId: 1);

        Assert.Null(goal);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesTitleTargetAmountAndStatus()
    {
        _goalRepo.Store.Add(new SavingsGoal { Id = 1, CustomerId = 1, AccountId = 10, Title = "Gammal titel", TargetAmount = 5000m, Status = "active" });

        var updateReq = new UpdateSavingsGoalRequest(
            Title: "Ny uppdaterad titel",
            TargetAmount: 12000m,
            Status: "paused");

        var updated = await _service.UpdateAsync(1, updateReq, customerId: 1);

        Assert.Equal("Ny uppdaterad titel", updated.Title);
        Assert.Equal(12000m, updated.TargetAmount);
        Assert.Equal("paused", updated.Status);
        Assert.NotNull(updated.UpdatedAt);
    }

    [Fact]
    public async Task UpdateAsync_InvalidStatus_ThrowsValidationException()
    {
        _goalRepo.Store.Add(new SavingsGoal { Id = 1, CustomerId = 1, AccountId = 10, Title = "Mål", TargetAmount = 5000m, Status = "active" });

        var updateReq = new UpdateSavingsGoalRequest(Status: "invalid_status");

        await Assert.ThrowsAsync<ValidationException>(() => _service.UpdateAsync(1, updateReq, customerId: 1));
    }

    [Fact]
    public async Task UpdateAsync_NotOwner_ThrowsNotFoundException()
    {
        _goalRepo.Store.Add(new SavingsGoal { Id = 1, CustomerId = 2, AccountId = 20, Title = "Mål kund 2", TargetAmount = 5000m, Status = "active" });

        var updateReq = new UpdateSavingsGoalRequest(Title: "Hack försök");

        await Assert.ThrowsAsync<NotFoundException>(() => _service.UpdateAsync(1, updateReq, customerId: 1));
    }

    [Fact]
    public async Task DeleteAsync_ValidGoal_DeletesGoalAndAssociatedPlannedTransaction()
    {
        _goalRepo.Store.Add(new SavingsGoal { Id = 1, CustomerId = 1, AccountId = 10, Title = "Mål", TargetAmount = 5000m, Status = "active" });
        _txRepo.Store.Add(new LedgerEntry { Id = 100, SavingsGoalId = 1, IsPlanned = true, Amount = 500m });

        var result = await _service.DeleteAsync(1, customerId: 1);

        Assert.True(result);
        Assert.Empty(_goalRepo.Store);
        Assert.Empty(_txRepo.Store); // Automation planned transaction cleaned up
    }

    [Fact]
    public async Task DeleteAsync_NotOwner_ThrowsNotFoundException()
    {
        _goalRepo.Store.Add(new SavingsGoal { Id = 1, CustomerId = 2, AccountId = 20, Title = "Mål kund 2", TargetAmount = 5000m, Status = "active" });

        await Assert.ThrowsAsync<NotFoundException>(() => _service.DeleteAsync(1, customerId: 1));
    }
}
