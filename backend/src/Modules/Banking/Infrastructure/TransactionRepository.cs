using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nordiska.BuildingBlocks.Database;
using Nordiska.BuildingBlocks.Database.Errors;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Domain;
using Nordiska.Modules.Banking.Infrastructure.Db;
using Npgsql;

namespace Nordiska.Modules.Banking.Infrastructure;

public sealed class TransactionRepository(BankingDbContext db) : ITransactionRepository
{
    public async Task<IEnumerable<LedgerEntry>> QueryAsync(long? accountId = null, CancellationToken cancellationToken = default)
    {
        var query = db.LedgerEntries.AsNoTracking().Where(l => !l.IsPlanned).AsQueryable();
        if (accountId.HasValue) query = query.Where(l => l.AccountId == accountId.Value);
        return await query.ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<LedgerEntry>> QueryPagedAsync(TransactionQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var query = db.LedgerEntries.AsNoTracking().AsQueryable();

        if (parameters.AccountIds != null && parameters.AccountIds.Count > 0)
        {
            query = query.Where(l => parameters.AccountIds.Contains(l.AccountId));
        }

        if (!string.IsNullOrWhiteSpace(parameters.Type))
        {
            var typeLower = parameters.Type.Trim().ToLowerInvariant();
            query = query.Where(l => l.Type.ToLower() == typeLower);
        }

        if (parameters.IsPlanned.HasValue)
        {
            query = query.Where(l => l.IsPlanned == parameters.IsPlanned.Value);
        }

        if (parameters.FromDate.HasValue)
        {
            query = query.Where(l => l.CreatedAt >= parameters.FromDate.Value);
        }

        if (parameters.ToDate.HasValue)
        {
            query = query.Where(l => l.CreatedAt <= parameters.ToDate.Value);
        }

        if (parameters.MinAmount.HasValue)
        {
            query = query.Where(l => l.Amount >= parameters.MinAmount.Value);
        }

        if (parameters.MaxAmount.HasValue)
        {
            query = query.Where(l => l.Amount <= parameters.MaxAmount.Value);
        }

        if (!string.IsNullOrWhiteSpace(parameters.SearchTerm))
        {
            var term = parameters.SearchTerm.Trim().ToLowerInvariant();
            query = query.Where(l => l.Type.ToLower().Contains(term) 
                                     || (l.Label != null && l.Label.ToLower().Contains(term))
                                     || l.Id.ToString().Contains(term) 
                                     || l.AccountId.ToString().Contains(term));
        }

        if (parameters.CorrelationId.HasValue)
        {
            query = query.Where(l => l.CorrelationId == parameters.CorrelationId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var isAsc = string.Equals(parameters.SortOrder, "asc", StringComparison.OrdinalIgnoreCase);
        var sortBy = parameters.SortBy?.Trim().ToLowerInvariant();

        query = sortBy switch
        {
            "amount" => isAsc ? query.OrderBy(l => l.Amount).ThenBy(l => l.Id) : query.OrderByDescending(l => l.Amount).ThenByDescending(l => l.Id),
            _ => isAsc ? query.OrderBy(l => l.CreatedAt).ThenBy(l => l.Id) : query.OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id)
        };

        var page = parameters.NormalizedPage;
        var pageSize = parameters.NormalizedPageSize;

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<LedgerEntry>.Create(items, totalCount, page, pageSize);
    }

    public Task<LedgerEntry?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => db.LedgerEntries.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    public async Task<List<LedgerEntry>> GetPendingPlannedTransactionsAsync(DateTime asOfUtc, CancellationToken cancellationToken = default)
    {
        return await db.LedgerEntries
            .Where(l => l.IsPlanned && l.PlannedDate.HasValue && l.PlannedDate.Value <= asOfUtc)
            .OrderBy(l => l.PlannedDate)
            .ThenBy(l => l.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<long> CreateAsync(LedgerEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.Id != 0) throw new ArgumentException("Only new ledger entries can be created.", nameof(entry));
        db.LedgerEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        return entry.Id;
    }

    public async Task<long> CreateLedgerEntry(LedgerEntry entry, CancellationToken cancellationToken = default)
    {
        ValidateLedgerEntry(entry);
        var posted = await RunSerializableAsync(() => BookLedgerEntryAsync(entry, cancellationToken), cancellationToken);
        entry.Id = posted.Id;
        entry.CreatedAt = posted.CreatedAt;
        return posted.Id;
    }

    public Task<LedgerEntry> CreateTransferAsync(TransferRequest request, CancellationToken cancellationToken = default)
    {
        ValidateTransfer(request);
        return RunSerializableAsync(() => BookTransferAsync(request, cancellationToken), cancellationToken);
    }

    public async Task<LedgerEntry?> ProcessPlannedTransactionAsync(
        long ledgerEntryId, DateTime asOfUtc, CancellationToken cancellationToken = default)
    {
        try
        {
            return await RunSerializableAsync(
                () => ProcessPlannedCoreAsync(ledgerEntryId, asOfUtc, cancellationToken), cancellationToken);
        }
        catch (ConflictException)
        {
            return null;
        }
    }

    public async Task<bool> UpdateAsync(LedgerEntry entry, CancellationToken cancellationToken = default)
    {
        var original = ValidatePlannedUpdate(entry);
        var desired = CopyEntry(entry);

        return await RunPlannedMutationAsync(async () =>
        {
            var rows = await MatchingEntry(original).ExecuteUpdateAsync(setters => setters
                .SetProperty(current => current.AccountId, desired.AccountId)
                .SetProperty(current => current.Type, desired.Type)
                .SetProperty(current => current.Amount, desired.Amount)
                .SetProperty(current => current.Label, desired.Label)
                .SetProperty(current => current.TargetAccountId, desired.TargetAccountId)
                .SetProperty(current => current.PlannedDate, desired.PlannedDate)
                .SetProperty(current => current.Repeating, desired.Repeating)
                .SetProperty(current => current.SavingsGoalId, desired.SavingsGoalId)
                .SetProperty(current => current.CorrelationId, desired.CorrelationId), cancellationToken);
            EnsureEntryUnchanged(rows);
            db.Entry(entry).State = EntityState.Detached;
            return true;
        }, entry, cancellationToken);
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var tracked = db.ChangeTracker.Entries<LedgerEntry>().SingleOrDefault(item => item.Entity.Id == id);
        var original = tracked is null ? null : (LedgerEntry)tracked.OriginalValues.ToObject();
        if (original is not null) EnsurePlanned(original);

        return await RunPlannedMutationAsync(async () =>
        {
            var current = await db.LedgerEntries.AsNoTracking().SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken);
            if (current is null)
            {
                if (original is not null) EnsureEntryUnchanged(0);
                return false;
            }
            EnsurePlanned(current);
            var rows = await MatchingEntry(original ?? current).ExecuteDeleteAsync(cancellationToken);
            EnsureEntryUnchanged(rows);
            var remaining = db.ChangeTracker.Entries<LedgerEntry>().SingleOrDefault(item => item.Entity.Id == id);
            if (remaining is not null) remaining.State = EntityState.Detached;
            return true;
        }, tracked?.Entity, cancellationToken);
    }

    private async Task<LedgerEntry> BookLedgerEntryAsync(LedgerEntry entry, CancellationToken cancellationToken)
    {
        var account = await db.SavingsAccounts.SingleOrDefaultAsync(
            account => account.Id == entry.AccountId, cancellationToken)
            ?? throw new NotFoundException($"Account {entry.AccountId} not found.");
        var balance = await db.LedgerEntries
            .Where(ledger => ledger.AccountId == entry.AccountId && !ledger.IsPlanned)
            .SumAsync(ledger => (decimal?)ledger.Amount, cancellationToken) ?? 0m;
        EnsureSufficientFunds(entry, balance);

        var posted = CopyEntry(entry);
        posted.CreatedAt = entry.CreatedAt == default ? DateTime.UtcNow : entry.CreatedAt;
        db.LedgerEntries.Add(posted);
        account.Balance = balance + posted.Amount;
        account.UpdatedAt = DateTime.UtcNow;
        return posted;
    }

    private async Task<LedgerEntry> BookTransferAsync(TransferRequest request, CancellationToken cancellationToken)
    {
        ValidateTransfer(request);
        var correlationId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var withdrawal = await BookLedgerEntryAsync(new LedgerEntry
        {
            AccountId = request.SourceAccountId, TargetAccountId = request.TargetAccountId,
            Type = "transfer", Amount = -request.Amount, Label = request.Label,
            CorrelationId = correlationId, CreatedAt = now
        }, cancellationToken);
        await BookLedgerEntryAsync(new LedgerEntry
        {
            AccountId = request.TargetAccountId, TargetAccountId = request.SourceAccountId,
            Type = "transfer", Amount = request.Amount, Label = request.Label,
            CorrelationId = correlationId, CreatedAt = now
        }, cancellationToken);
        return withdrawal;
    }

    private async Task<LedgerEntry?> ProcessPlannedCoreAsync(long id, DateTime asOfUtc, CancellationToken cancellationToken)
    {
        var plan = await db.LedgerEntries.SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken);
        if (plan is null || !plan.IsPlanned || !plan.PlannedDate.HasValue || plan.PlannedDate.Value > asOfUtc)
            return null;
        ValidatePositiveAmount(plan.Amount);

        var goal = plan.SavingsGoalId.HasValue
            ? await db.SavingsGoals.SingleOrDefaultAsync(goal => goal.Id == plan.SavingsGoalId.Value, cancellationToken)
            : null;
        if (string.Equals(goal?.Status, "paused", StringComparison.OrdinalIgnoreCase))
        {
            var nextPausedDate = NextPlannedDate(plan);
            if (nextPausedDate.HasValue) plan.PlannedDate = nextPausedDate;
            return null;
        }

        LedgerEntry result;
        if (plan.TargetAccountId.HasValue || string.Equals(plan.Type, "transfer", StringComparison.OrdinalIgnoreCase))
        {
            if (!plan.TargetAccountId.HasValue)
                throw new ArgumentException("A planned transfer must have a target account.", nameof(plan));
            result = await BookTransferAsync(
                new TransferRequest(plan.AccountId, plan.TargetAccountId.Value, plan.Amount, plan.Label), cancellationToken);
        }
        else
        {
            var posting = new LedgerEntry
            {
                AccountId = plan.AccountId, Type = plan.Type,
                Amount = string.Equals(plan.Type, "deposit", StringComparison.OrdinalIgnoreCase) ? plan.Amount : -plan.Amount,
                Label = plan.Label, SavingsGoalId = plan.SavingsGoalId, CreatedAt = DateTime.UtcNow
            };
            ValidateLedgerEntry(posting);
            result = await BookLedgerEntryAsync(posting, cancellationToken);
        }

        if (goal is not null)
        {
            goal.CurrentAmount += plan.Amount;
            goal.UpdatedAt = DateTime.UtcNow;
            if (goal.CurrentAmount >= goal.TargetAmount) goal.Status = "completed";
        }
        var nextDate = NextPlannedDate(plan);
        if (nextDate.HasValue) plan.PlannedDate = nextDate;
        else db.LedgerEntries.Remove(plan);
        return result;
    }

    private async Task<T> RunSerializableAsync<T>(
        Func<Task<T>> operation, CancellationToken cancellationToken, LedgerEntry? pendingEntry = null)
    {
        ValidateTransactionState(pendingEntry);
        const int maxAttempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            db.ChangeTracker.Clear();
            // This is basically "BEGIN" in the query - it starts the transaction
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            try
            {
                var result = await operation();
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch (Exception exception) when (attempt < maxAttempts && IsSerializationFailure(exception))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                db.ChangeTracker.Clear();
                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken);
            }
            catch (Exception exception) when (IsSerializationFailure(exception))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                db.ChangeTracker.Clear();
                throw new ConflictException("Concurrent account activity prevented this operation. Please retry.");
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                db.ChangeTracker.Clear();
                throw;
            }
        }
    }

    private Task<T> RunPlannedMutationAsync<T>(Func<Task<T>> operation, LedgerEntry? pendingEntry, CancellationToken cancellationToken)
        => db.Database.CurrentTransaction is not null
            ? operation()
            : RunSerializableAsync(operation, cancellationToken, pendingEntry);

    private IQueryable<LedgerEntry> MatchingEntry(LedgerEntry original)
        => db.LedgerEntries.Where(current => current.Id == original.Id
            && current.AccountId == original.AccountId && current.Type == original.Type
            && current.Amount == original.Amount && current.Label == original.Label
            && current.TargetAccountId == original.TargetAccountId && current.IsPlanned == original.IsPlanned
            && current.PlannedDate == original.PlannedDate && current.Repeating == original.Repeating
            && current.SavingsGoalId == original.SavingsGoalId && current.CorrelationId == original.CorrelationId
            && current.CreatedAt == original.CreatedAt);

    private static LedgerEntry CopyEntry(LedgerEntry entry) => new()
    {
        Id = entry.Id, AccountId = entry.AccountId, Type = entry.Type, Amount = entry.Amount,
        Label = entry.Label, TargetAccountId = entry.TargetAccountId, IsPlanned = entry.IsPlanned,
        PlannedDate = entry.PlannedDate, Repeating = entry.Repeating, SavingsGoalId = entry.SavingsGoalId,
        CorrelationId = entry.CorrelationId, CreatedAt = entry.CreatedAt
    };

    private static DateTime? NextPlannedDate(LedgerEntry entry) => entry.Repeating?.Trim().ToLowerInvariant() switch
    {
        "week" => entry.PlannedDate?.AddDays(7),
        "month" => entry.PlannedDate?.AddMonths(1),
        "year" => entry.PlannedDate?.AddYears(1),
        _ => null
    };

    // a helper method for the ledger entries - validates input
    private static void ValidateLedgerEntry(LedgerEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.Id != 0) throw new ArgumentException("Only new ledger entries can be created.", nameof(entry));
        var isDeposit = string.Equals(entry.Type, "deposit", StringComparison.OrdinalIgnoreCase);
        var isWithdrawal = string.Equals(entry.Type, "withdrawal", StringComparison.OrdinalIgnoreCase)
            || string.Equals(entry.Type, "withdraw", StringComparison.OrdinalIgnoreCase);
        if (entry.IsPlanned || entry.TargetAccountId.HasValue || (!isDeposit && !isWithdrawal))
            throw new ArgumentException("Only executed deposits and withdrawals can be booked here. Transfers require both entries in one transaction.", nameof(entry));
        ValidatePositiveAmount(Math.Abs(entry.Amount));
        if ((isDeposit && entry.Amount < 0) || (isWithdrawal && entry.Amount > 0))
            throw new ArgumentException("Deposits require a positive amount and withdrawals require a negative amount.", nameof(entry));
    }

    private LedgerEntry ValidatePlannedUpdate(LedgerEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        EnsurePlanned(entry);
        ValidatePositiveAmount(entry.Amount);
        var tracked = db.Entry(entry);
        if (tracked.State == EntityState.Detached)
            throw new InvalidOperationException("Load the planned entry through this repository before editing it so its original values can be checked.");

        var original = (LedgerEntry)tracked.OriginalValues.ToObject();
        EnsurePlanned(original);
        return original;
    }

    private void ValidateTransactionState(LedgerEntry? pendingEntry)
    {
        if (db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("This operation must own its transaction so it can roll back and retry the complete posting.");
        if (db.ChangeTracker.Entries().Any(entry => !ReferenceEquals(entry.Entity, pendingEntry)
            && entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Save pending changes before booking a ledger entry.");
    }

    private static void ValidateTransfer(TransferRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidatePositiveAmount(request.Amount);
        if (request.SourceAccountId == request.TargetAccountId)
            throw new ArgumentException("Source and target accounts cannot be the same.", nameof(request));
    }

    private static void ValidatePositiveAmount(decimal amount)
    {
        if (amount <= 0 || decimal.Round(amount, 2) != amount)
            throw new ArgumentException("The amount must be positive and have at most two decimal places.", nameof(amount));
    }

    private static void EnsurePlanned(LedgerEntry entry)
    {
        if (!entry.IsPlanned)
            throw new ConflictException("Posted ledger entries cannot be updated or deleted. Book a correcting entry instead.");
    }

    private static void EnsureEntryUnchanged(int rows)
    {
        if (rows != 1)
            throw new ConflictException("The planned transaction changed or was processed. Reload it before trying again.");
    }

    private static void EnsureSufficientFunds(LedgerEntry entry, decimal balance)
    {
        if (entry.Amount < 0 && balance + entry.Amount < 0)
            throw new ConflictException($"Insufficient funds. Current balance is {balance:N2}, requested withdrawal is {-entry.Amount:N2}.");
    }

    private static bool IsSerializationFailure(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres &&
                postgres.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected)
                return true;
        }
        return false;
    }

    public async Task<LedgerEntry?> GetPlannedTransactionByGoalIdAsync(long savingsGoalId, CancellationToken cancellationToken = default)
    {
        return await db.LedgerEntries
            .FirstOrDefaultAsync(l => l.SavingsGoalId == savingsGoalId && l.IsPlanned, cancellationToken);
    }

    public async Task<IEnumerable<LedgerEntry>> GetByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken = default)
    {
        return await db.LedgerEntries
            .AsNoTracking()
            .Where(l => l.CorrelationId == correlationId)
            .OrderBy(l => l.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}