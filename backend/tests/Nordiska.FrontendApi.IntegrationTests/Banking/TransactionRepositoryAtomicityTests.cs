using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Nordiska.BuildingBlocks.Database.Errors;
using Nordiska.FrontendApi.IntegrationTests.Postgres;
using Nordiska.Modules.Banking.Domain;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Infrastructure;
using Nordiska.Modules.Banking.Infrastructure.Db;
using Npgsql;

namespace Nordiska.FrontendApi.IntegrationTests.Banking;

[Collection(PostgresCollection.Name)]
public sealed class TransactionRepositoryAtomicityTests(PostgresAuthWebApplicationFactory factory) : IAsyncLifetime
{
    private readonly List<long> _customerIds = new();

    [PostgresFact]
    public async Task Deposit_BooksEntryAndReplacesCachedBalanceWithLedgerTotal()
    {
        var accountId = await SeedAccountAsync();
        await using var db = CreateContext();
        var account = await db.SavingsAccounts.SingleAsync(account => account.Id == accountId);
        account.Balance = 999m;
        await db.SaveChangesAsync();
        var entry = Entry(accountId, 20m);

        var id = await new TransactionRepository(db).CreateLedgerEntry(entry);

        Assert.True(id > 0);
        Assert.Equal(id, entry.Id);
        Assert.NotEqual(default, entry.CreatedAt);
        await AssertBalanceAsync(accountId, 120m, 2);
    }

    [PostgresFact]
    public async Task Withdrawal_WithInsufficientFunds_WritesNothing()
    {
        var accountId = await SeedAccountAsync();
        await using var db = CreateContext();
        var entry = Entry(accountId, -120m);

        await Assert.ThrowsAsync<ConflictException>(() => new TransactionRepository(db).CreateLedgerEntry(entry));

        Assert.Equal(0, entry.Id);
        await AssertBalanceAsync(accountId, 100m, 1);
    }

    [PostgresFact]
    public async Task FailureBeforeCommit_RollsBackBothLedgerAndBalance()
    {
        var accountId = await SeedAccountAsync();
        await using var db = CreateContext(new FailCommitInterceptor());
        var entry = Entry(accountId, 20m);

        await Assert.ThrowsAsync<InvalidOperationException>(() => new TransactionRepository(db).CreateLedgerEntry(entry));

        Assert.Equal(0, entry.Id);
        await AssertBalanceAsync(accountId, 100m, 1);
    }

    [PostgresFact]
    public async Task ConcurrentWithdrawals_OnlyOneCanSpendAvailableFunds()
    {
        var accountId = await SeedAccountAsync();
        var gate = new ReadGate();
        await using var first = CreateContext(new OverlapReadsInterceptor(gate));
        await using var second = CreateContext(new OverlapReadsInterceptor(gate));

        async Task<bool> WithdrawAsync(BankingDbContext db)
        {
            try
            {
                await new TransactionRepository(db).CreateLedgerEntry(Entry(accountId, -80m));
                return true;
            }
            catch (ConflictException)
            {
                return false;
            }
        }

        var results = await Task.WhenAll(WithdrawAsync(first), WithdrawAsync(second));

        Assert.Single(results, succeeded => succeeded);
        await AssertBalanceAsync(accountId, 20m, 2);
    }

    [PostgresFact]
    public async Task ConcurrentDeposits_PreserveBothPostingsAndCachedBalance()
    {
        var accountId = await SeedAccountAsync();
        var gate = new ReadGate();
        await using var first = CreateContext(new OverlapReadsInterceptor(gate));
        await using var second = CreateContext(new OverlapReadsInterceptor(gate));

        await Task.WhenAll(
            new TransactionRepository(first).CreateLedgerEntry(Entry(accountId, 20m)),
            new TransactionRepository(second).CreateLedgerEntry(Entry(accountId, 30m)));

        await AssertBalanceAsync(accountId, 150m, 3);
    }

    [PostgresFact]
    public async Task SerializationFailure_RetriesWholePostingWithoutDuplicateEntries()
    {
        var accountId = await SeedAccountAsync();
        await using var db = CreateContext(new FailWriteInterceptor(serializationFailures: 2));
        var entry = Entry(accountId, 20m);

        var id = await new TransactionRepository(db).CreateLedgerEntry(entry);

        Assert.Equal(id, entry.Id);
        await AssertBalanceAsync(accountId, 120m, 2);
    }

    [PostgresFact]
    public async Task RepeatedSerializationFailures_StopAndLeaveNoChanges()
    {
        var accountId = await SeedAccountAsync();
        var failure = new FailWriteInterceptor(serializationFailures: int.MaxValue);
        await using var db = CreateContext(failure);
        var entry = Entry(accountId, 20m);

        var error = await Record.ExceptionAsync(() => new TransactionRepository(db).CreateLedgerEntry(entry));

        Assert.NotNull(error);
        Assert.IsType<ConflictException>(error);
        Assert.Equal(3, failure.Attempts);
        Assert.Equal(0, entry.Id);
        await AssertBalanceAsync(accountId, 100m, 1);
    }

    [PostgresFact]
    public async Task PlannedOrTransferEntries_AreRejectedWithoutPosting()
    {
        var accountId = await SeedAccountAsync();
        await using var db = CreateContext();
        var repository = new TransactionRepository(db);
        var planned = Entry(accountId, 20m);
        planned.IsPlanned = true;
        var transfer = Entry(accountId, -20m);
        transfer.Type = "transfer";
        transfer.TargetAccountId = accountId + 1;

        await Assert.ThrowsAsync<ArgumentException>(() => repository.CreateLedgerEntry(planned));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.CreateLedgerEntry(transfer));

        await AssertBalanceAsync(accountId, 100m, 1);
    }

    [PostgresFact]
    public async Task ExistingTransaction_IsRejectedBeforeAnyPosting()
    {
        var accountId = await SeedAccountAsync();
        await using var db = CreateContext();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        await Assert.ThrowsAsync<InvalidOperationException>(() => new TransactionRepository(db).CreateLedgerEntry(Entry(accountId, 20m)));

        await transaction.CommitAsync();
        await AssertBalanceAsync(accountId, 100m, 1);
    }

    [PostgresFact]
    public async Task PendingTrackedChanges_AreRejectedAndPreserved()
    {
        var accountId = await SeedAccountAsync();
        await using var db = CreateContext();
        var account = await db.SavingsAccounts.SingleAsync(account => account.Id == accountId);
        account.AccountName = "Unsaved caller change";

        await Assert.ThrowsAsync<InvalidOperationException>(() => new TransactionRepository(db).CreateLedgerEntry(Entry(accountId, 20m)));

        Assert.Equal("Unsaved caller change", account.AccountName);
        Assert.Equal(EntityState.Modified, db.Entry(account).State);
        await AssertBalanceAsync(accountId, 100m, 1);
    }

    [PostgresFact]
    public async Task PlannedDeposits_DoNotCountAsAvailableFunds()
    {
        var accountId = await SeedAccountAsync();
        await using var db = CreateContext();
        db.LedgerEntries.Add(new LedgerEntry
        {
            AccountId = accountId, Type = "deposit", Amount = 500m, IsPlanned = true,
            PlannedDate = DateTime.UtcNow.AddDays(1), CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ConflictException>(() => new TransactionRepository(db).CreateLedgerEntry(Entry(accountId, -200m)));

        await AssertBalanceAsync(accountId, 100m, 1);
        await using var check = CreateContext();
        Assert.Single(await check.LedgerEntries.Where(entry => entry.AccountId == accountId && entry.IsPlanned).ToListAsync());
    }

    [PostgresFact]
    public async Task Transfer_BooksBothSidesAndBalancesTogether()
    {
        var source = await SeedAccountAsync();
        var target = await SeedAccountAsync();
        await using var db = CreateContext();

        var debit = await new TransactionRepository(db).CreateTransferAsync(new TransferRequest(source, target, 80m));

        Assert.True(debit.Id > 0);
        await AssertBalanceAsync(source, 20m, 2);
        await AssertBalanceAsync(target, 180m, 2);
        await using var check = CreateContext();
        var pair = await check.LedgerEntries.Where(entry => entry.CorrelationId == debit.CorrelationId).ToListAsync();
        Assert.Equal(2, pair.Count);
        Assert.Equal(0m, pair.Sum(entry => entry.Amount));
    }

    [PostgresFact]
    public async Task Transfer_FailureBeforeCommit_RollsBackBothSides()
    {
        var source = await SeedAccountAsync();
        var target = await SeedAccountAsync();
        await using var db = CreateContext(new FailCommitInterceptor());

        await Assert.ThrowsAsync<InvalidOperationException>(() => new TransactionRepository(db)
            .CreateTransferAsync(new TransferRequest(source, target, 80m)));

        await AssertBalanceAsync(source, 100m, 1);
        await AssertBalanceAsync(target, 100m, 1);
    }

    [PostgresFact]
    public async Task ConcurrentTransfers_CannotOverdrawSource()
    {
        var source = await SeedAccountAsync();
        var target = await SeedAccountAsync();
        var gate = new ReadGate();
        await using var first = CreateContext(new OverlapReadsInterceptor(gate));
        await using var second = CreateContext(new OverlapReadsInterceptor(gate));
        async Task<bool> TransferAsync(BankingDbContext db)
        {
            try
            {
                await new TransactionRepository(db).CreateTransferAsync(new TransferRequest(source, target, 80m));
                return true;
            }
            catch (ConflictException) { return false; }
        }

        var results = await Task.WhenAll(TransferAsync(first), TransferAsync(second));

        Assert.Single(results, succeeded => succeeded);
        await AssertBalanceAsync(source, 20m, 2);
        await AssertBalanceAsync(target, 180m, 2);
    }

    [PostgresFact]
    public async Task ConcurrentPlannedProcessing_ExecutesOccurrenceOnce()
    {
        var account = await SeedAccountAsync();
        var planId = await SeedPlanAsync(account, recurring: true);
        var asOfUtc = DateTime.UtcNow.Date.AddDays(2);
        var gate = new ReadGate();
        await using var first = CreateContext(new OverlapReadsInterceptor(gate));
        await using var second = CreateContext(new OverlapReadsInterceptor(gate));

        var results = await Task.WhenAll(
            new TransactionRepository(first).ProcessPlannedTransactionAsync(planId, asOfUtc),
            new TransactionRepository(second).ProcessPlannedTransactionAsync(planId, asOfUtc));

        Assert.Single(results, result => result is not null);
        await AssertBalanceAsync(account, 90m, 2);
        await using var check = CreateContext();
        Assert.Equal(DateTime.UtcNow.Date.AddDays(1).AddMonths(1),
            (await check.LedgerEntries.SingleAsync(entry => entry.Id == planId)).PlannedDate);
    }

    [PostgresFact]
    public async Task PlannedProcessing_FailureBeforeCommit_PreservesPlanAndFunds()
    {
        var account = await SeedAccountAsync();
        var planId = await SeedPlanAsync(account);
        await using var db = CreateContext(new FailCommitInterceptor());

        await Assert.ThrowsAsync<InvalidOperationException>(() => new TransactionRepository(db)
            .ProcessPlannedTransactionAsync(planId, DateTime.UtcNow.Date.AddDays(2)));

        await AssertBalanceAsync(account, 100m, 1);
        await using var check = CreateContext();
        Assert.True((await check.LedgerEntries.SingleAsync(entry => entry.Id == planId)).IsPlanned);
    }

    [PostgresFact]
    public async Task CancellationRacingWithProcessing_CancelsOrExecutesOnce()
    {
        var account = await SeedAccountAsync();
        var planId = await SeedPlanAsync(account);
        var gate = new ReadGate();
        await using var processor = CreateContext(new OverlapReadsInterceptor(gate));
        await using var canceller = CreateContext(new OverlapReadsInterceptor(gate));

        var processing = new TransactionRepository(processor).ProcessPlannedTransactionAsync(planId, DateTime.UtcNow.Date.AddDays(2));
        var cancellation = new TransactionRepository(canceller).DeleteAsync(planId);
        await Task.WhenAll(processing, cancellation);

        var executed = processing.Result is not null;
        Assert.Equal(!executed, cancellation.Result);
        await AssertBalanceAsync(account, executed ? 90m : 100m, executed ? 2 : 1);
    }

    [PostgresFact]
    public async Task UpdatingStalePlan_CannotOverwriteAdvancedSchedule()
    {
        var account = await SeedAccountAsync();
        var planId = await SeedPlanAsync(account, recurring: true);
        await using var stale = CreateContext();
        var repository = new TransactionRepository(stale);
        var plan = (await repository.GetByIdAsync(planId))!;
        await using (var processor = CreateContext())
        {
            await new TransactionRepository(processor).ProcessPlannedTransactionAsync(planId, DateTime.UtcNow.Date.AddDays(2));
        }
        plan.Amount = 25m;

        await Assert.ThrowsAsync<ConflictException>(() => repository.UpdateAsync(plan));

        await using var check = CreateContext();
        var stored = await check.LedgerEntries.SingleAsync(entry => entry.Id == planId);
        Assert.Equal(10m, stored.Amount);
        Assert.Equal(DateTime.UtcNow.Date.AddDays(1).AddMonths(1), stored.PlannedDate);
    }

    [PostgresFact]
    public async Task PostedEntries_CannotBeUpdatedOrDeleted()
    {
        var account = await SeedAccountAsync();
        await using var db = CreateContext();
        var repository = new TransactionRepository(db);
        var entry = await db.LedgerEntries.SingleAsync(entry => entry.AccountId == account);
        entry.Label = "changed";

        await Assert.ThrowsAsync<ConflictException>(() => repository.UpdateAsync(entry));
        db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<ConflictException>(() => repository.DeleteAsync(entry.Id));

        await AssertBalanceAsync(account, 100m, 1);
    }

    [PostgresFact]
    public async Task UpdatingPlan_SavesChangesWithoutMovingFunds()
    {
        var account = await SeedAccountAsync();
        var planId = await SeedPlanAsync(account);
        await using var db = CreateContext();
        var repository = new TransactionRepository(db);
        var plan = (await repository.GetByIdAsync(planId))!;
        plan.Amount = 25m;
        plan.Label = "New schedule";
        plan.PlannedDate = DateTime.UtcNow.Date.AddDays(3);

        Assert.True(await repository.UpdateAsync(plan));

        await using var check = CreateContext();
        var stored = await check.LedgerEntries.SingleAsync(entry => entry.Id == planId);
        Assert.Equal(25m, stored.Amount);
        Assert.Equal("New schedule", stored.Label);
        Assert.Equal(DateTime.UtcNow.Date.AddDays(3), stored.PlannedDate);
        await AssertBalanceAsync(account, 100m, 1);
    }

    [PostgresFact]
    public async Task PlanDeletion_InCallerTransaction_CanBeRolledBack()
    {
        var account = await SeedAccountAsync();
        var planId = await SeedPlanAsync(account);
        await using (var db = CreateContext())
        {
            var repository = new TransactionRepository(db);
            _ = await repository.GetByIdAsync(planId);
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            Assert.True(await repository.DeleteAsync(planId));
            await transaction.RollbackAsync();
        }

        await using var check = CreateContext();
        Assert.True((await check.LedgerEntries.SingleAsync(entry => entry.Id == planId)).IsPlanned);
    }

    [PostgresFact]
    public async Task StaleCancellation_CannotDeleteAdvancedRecurringPlan()
    {
        var account = await SeedAccountAsync();
        var planId = await SeedPlanAsync(account, recurring: true);
        await using var stale = CreateContext();
        var repository = new TransactionRepository(stale);
        _ = await repository.GetByIdAsync(planId);
        await using (var processor = CreateContext())
            await new TransactionRepository(processor).ProcessPlannedTransactionAsync(planId, DateTime.UtcNow.Date.AddDays(2));

        await Assert.ThrowsAsync<ConflictException>(() => repository.DeleteAsync(planId));

        await using var check = CreateContext();
        Assert.Equal(DateTime.UtcNow.Date.AddDays(1).AddMonths(1),
            (await check.LedgerEntries.SingleAsync(entry => entry.Id == planId)).PlannedDate);
    }

    [PostgresFact]
    public async Task PlannedGoalTransfer_UpdatesFundsGoalAndPlanTogether()
    {
        var source = await SeedAccountAsync();
        var target = await SeedAccountAsync();
        var goalId = await SeedGoalAsync(target);
        var planId = await SeedPlanAsync(source, targetAccountId: target, goalId: goalId);
        await using var db = CreateContext();

        var result = await new TransactionRepository(db).ProcessPlannedTransactionAsync(planId, DateTime.UtcNow.Date.AddDays(2));

        Assert.NotNull(result);
        await AssertBalanceAsync(source, 90m, 2);
        await AssertBalanceAsync(target, 110m, 2);
        await using var check = CreateContext();
        var goal = await check.SavingsGoals.SingleAsync(goal => goal.Id == goalId);
        Assert.Equal(10m, goal.CurrentAmount);
        Assert.Equal("completed", goal.Status);
        Assert.Null(await check.LedgerEntries.SingleOrDefaultAsync(entry => entry.Id == planId));
    }

    [PostgresFact]
    public async Task PlannedGoalTransfer_Failure_RollsBackGoalFundsAndSchedule()
    {
        var source = await SeedAccountAsync();
        var target = await SeedAccountAsync();
        var goalId = await SeedGoalAsync(target);
        var planId = await SeedPlanAsync(source, targetAccountId: target, goalId: goalId);
        await using var db = CreateContext(new FailCommitInterceptor());

        await Assert.ThrowsAsync<InvalidOperationException>(() => new TransactionRepository(db)
            .ProcessPlannedTransactionAsync(planId, DateTime.UtcNow.Date.AddDays(2)));

        await AssertBalanceAsync(source, 100m, 1);
        await AssertBalanceAsync(target, 100m, 1);
        await using var check = CreateContext();
        var goal = await check.SavingsGoals.SingleAsync(goal => goal.Id == goalId);
        Assert.Equal(0m, goal.CurrentAmount);
        Assert.Equal("active", goal.Status);
        Assert.True((await check.LedgerEntries.SingleAsync(entry => entry.Id == planId)).IsPlanned);
    }

    [PostgresFact]
    public async Task PausedGoal_AdvancesScheduleWithoutMovingFunds()
    {
        var source = await SeedAccountAsync();
        var target = await SeedAccountAsync();
        var goalId = await SeedGoalAsync(target, "paused");
        var planId = await SeedPlanAsync(source, recurring: true, targetAccountId: target, goalId: goalId);
        await using var db = CreateContext();

        Assert.Null(await new TransactionRepository(db).ProcessPlannedTransactionAsync(planId, DateTime.UtcNow.Date.AddDays(2)));

        await AssertBalanceAsync(source, 100m, 1);
        await AssertBalanceAsync(target, 100m, 1);
        await using var check = CreateContext();
        Assert.Equal(0m, (await check.SavingsGoals.SingleAsync(goal => goal.Id == goalId)).CurrentAmount);
        Assert.Equal(DateTime.UtcNow.Date.AddDays(1).AddMonths(1),
            (await check.LedgerEntries.SingleAsync(entry => entry.Id == planId)).PlannedDate);
    }

    private async Task<long> SeedGoalAsync(long targetAccountId, string status = "active")
    {
        await using var db = CreateContext();
        var target = await db.SavingsAccounts.SingleAsync(account => account.Id == targetAccountId);
        var goal = new SavingsGoal
        {
            AccountId = targetAccountId, CustomerId = target.CustomerId, Title = "Test goal",
            TargetAmount = 10m, Status = status, CreatedAt = DateTime.UtcNow
        };
        db.SavingsGoals.Add(goal);
        await db.SaveChangesAsync();
        return goal.Id;
    }

    private async Task<long> SeedPlanAsync(long accountId, bool recurring = false, long? targetAccountId = null, long? goalId = null)
    {
        await using var db = CreateContext();
        var plan = new LedgerEntry
        {
            AccountId = accountId, Type = targetAccountId.HasValue ? "transfer" : "withdrawal", Amount = 10m,
            TargetAccountId = targetAccountId, SavingsGoalId = goalId,
            IsPlanned = true, PlannedDate = DateTime.UtcNow.Date.AddDays(1),
            Repeating = recurring ? "month" : null, CreatedAt = DateTime.UtcNow
        };
        db.LedgerEntries.Add(plan);
        await db.SaveChangesAsync();
        return plan.Id;
    }

    private async Task<long> SeedAccountAsync()
    {
        var customer = await PostgresTestData.CreateCustomerAsync(factory.Services, "ledger");
        _customerIds.Add(customer.Id);
        await using var db = CreateContext();
        var config = await db.AccountTypeConfigs.AsNoTracking().FirstAsync();
        var account = new SavingsAccount
        {
            CustomerId = customer.Id, AccountNumber = $"T-{Guid.NewGuid():N}",
            AccountType = config.AccountType, Balance = 100m, CreatedAt = DateTime.UtcNow
        };
        db.SavingsAccounts.Add(account);
        db.LedgerEntries.Add(new LedgerEntry
        {
            Account = account, Type = "deposit", Amount = 100m, CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return account.Id;
    }

    private static LedgerEntry Entry(long accountId, decimal amount)
        => new() { AccountId = accountId, Type = amount > 0 ? "deposit" : "withdrawal", Amount = amount };

    private async Task AssertBalanceAsync(long accountId, decimal balance, int count)
    {
        await using var db = CreateContext();
        var entries = await db.LedgerEntries.Where(entry => entry.AccountId == accountId && !entry.IsPlanned).ToListAsync();
        Assert.Equal(count, entries.Count);
        Assert.Equal(balance, entries.Sum(entry => entry.Amount));
        Assert.Equal(balance, (await db.SavingsAccounts.SingleAsync(account => account.Id == accountId)).Balance);
    }

    private BankingDbContext CreateContext(params IInterceptor[] interceptors)
    {
        using var scope = factory.Services.CreateScope();
        var connectionString = scope.ServiceProvider.GetRequiredService<BankingDbContext>()
            .Database.GetDbConnection().ConnectionString;
        return new BankingDbContext(new DbContextOptionsBuilder<BankingDbContext>()
            .UseNpgsql(connectionString).AddInterceptors(interceptors).Options);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync()
    {
        await using (var db = CreateContext())
            await db.SavingsGoals.Where(goal => _customerIds.Contains(goal.CustomerId)).ExecuteDeleteAsync();
        await PostgresTestData.DeleteCustomersAsync(factory.Services, _customerIds);
    }

    private sealed class ReadGate
    {
        private int _arrivals;
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task ArriveAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _arrivals) == 2) _ready.TrySetResult();
            await _ready.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
        }
    }

    private sealed class OverlapReadsInterceptor(ReadGate gate) : DbCommandInterceptor
    {
        private bool _hasWaited;
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (!_hasWaited && command.CommandText.StartsWith("SELECT", StringComparison.Ordinal)
                && command.CommandText.Contains("ledger_entries", StringComparison.Ordinal))
            {
                _hasWaited = true;
                await gate.ArriveAsync(cancellationToken);
            }
            return result;
        }
    }

    private sealed class FailCommitInterceptor : DbTransactionInterceptor
    {
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(
            DbTransaction transaction, TransactionEventData eventData, InterceptionResult result,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Injected failure after saving but before commit");
    }

    private sealed class FailWriteInterceptor(int serializationFailures) : DbCommandInterceptor
    {
        private int _failuresLeft = serializationFailures;
        public int Attempts { get; private set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("UPDATE", StringComparison.Ordinal)
                && command.CommandText.Contains("savings_accounts", StringComparison.Ordinal))
            {
                Attempts++;
                if (_failuresLeft-- <= 0)
                    return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
                throw new PostgresException("Injected serialization failure", "ERROR", "ERROR", PostgresErrorCodes.SerializationFailure);
            }
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
