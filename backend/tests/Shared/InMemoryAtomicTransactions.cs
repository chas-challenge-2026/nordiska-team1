using Nordiska.BuildingBlocks.Database.Errors;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Domain;

namespace Nordiska.Tests.Shared;

public abstract class InMemoryAtomicTransactions
{
    public ISavingsAccountRepository Accounts { get; set; } = null!;
    public ISavingsGoalRepository? Goals { get; set; }
    private ITransactionRepository Ledger => (ITransactionRepository)this;

    public async Task<long> CreateLedgerEntry(LedgerEntry entry, CancellationToken cancellationToken = default)
    {
        var account = await Accounts.GetByIdAsync(entry.AccountId, cancellationToken)
            ?? throw new NotFoundException("Account not found.");
        var balance = (await Ledger.QueryAsync(entry.AccountId, cancellationToken)).Sum(item => item.Amount);
        if (entry.Amount < 0 && balance + entry.Amount < 0) throw new ConflictException("Insufficient funds.");
        var id = await Ledger.CreateAsync(entry, cancellationToken);
        account.Balance = balance + entry.Amount;
        account.UpdatedAt = DateTime.UtcNow;
        await Accounts.UpdateAsync(account, cancellationToken);
        return id;
    }

    public async Task<LedgerEntry> CreateTransferAsync(TransferRequest request, CancellationToken cancellationToken = default)
    {
        _ = await Accounts.GetByIdAsync(request.SourceAccountId, cancellationToken)
            ?? throw new NotFoundException("Source account not found.");
        _ = await Accounts.GetByIdAsync(request.TargetAccountId, cancellationToken)
            ?? throw new NotFoundException("Target account not found.");
        var balance = (await Ledger.QueryAsync(request.SourceAccountId, cancellationToken)).Sum(item => item.Amount);
        if (balance < request.Amount) throw new ConflictException("Insufficient funds.");
        var correlationId = Guid.NewGuid();
        var withdrawal = new LedgerEntry
        {
            AccountId = request.SourceAccountId, TargetAccountId = request.TargetAccountId,
            Type = "transfer", Amount = -request.Amount, Label = request.Label,
            CorrelationId = correlationId, CreatedAt = DateTime.UtcNow
        };
        await CreateLedgerEntry(withdrawal, cancellationToken);
        await CreateLedgerEntry(new LedgerEntry
        {
            AccountId = request.TargetAccountId, TargetAccountId = request.SourceAccountId,
            Type = "transfer", Amount = request.Amount, Label = request.Label,
            CorrelationId = correlationId, CreatedAt = withdrawal.CreatedAt
        }, cancellationToken);
        return withdrawal;
    }

    public async Task<LedgerEntry?> ProcessPlannedTransactionAsync(long id, DateTime asOfUtc, CancellationToken cancellationToken = default)
    {
        var plan = await Ledger.GetByIdAsync(id, cancellationToken);
        if (plan is null || !plan.IsPlanned || !plan.PlannedDate.HasValue || plan.PlannedDate.Value > asOfUtc) return null;
        var goal = plan.SavingsGoalId.HasValue && Goals is not null
            ? await Goals.GetByIdAsync(plan.SavingsGoalId.Value, cancellationToken) : null;
        if (string.Equals(goal?.Status, "paused", StringComparison.OrdinalIgnoreCase))
        {
            var pausedDate = NextDate(plan);
            if (pausedDate.HasValue)
            {
                plan.PlannedDate = pausedDate;
                await Ledger.UpdateAsync(plan, cancellationToken);
            }
            return null;
        }
        try
        {
            LedgerEntry result;
            if (plan.TargetAccountId.HasValue)
                result = await CreateTransferAsync(new TransferRequest(plan.AccountId, plan.TargetAccountId.Value, plan.Amount, plan.Label), cancellationToken);
            else
            {
                result = new LedgerEntry
                {
                    AccountId = plan.AccountId, Type = plan.Type,
                    Amount = string.Equals(plan.Type, "deposit", StringComparison.OrdinalIgnoreCase) ? plan.Amount : -plan.Amount,
                    Label = plan.Label, CreatedAt = DateTime.UtcNow
                };
                await CreateLedgerEntry(result, cancellationToken);
            }
            if (goal is not null)
            {
                goal.CurrentAmount += plan.Amount;
                goal.UpdatedAt = DateTime.UtcNow;
                if (goal.CurrentAmount >= goal.TargetAmount) goal.Status = "completed";
                await Goals!.UpdateAsync(goal, cancellationToken);
            }
            var nextDate = NextDate(plan);
            if (nextDate.HasValue)
            {
                plan.PlannedDate = nextDate;
                await Ledger.UpdateAsync(plan, cancellationToken);
            }
            else await Ledger.DeleteAsync(plan.Id, cancellationToken);
            return result;
        }
        catch (ConflictException) { return null; }
    }

    private static DateTime? NextDate(LedgerEntry entry) => entry.Repeating?.Trim().ToLowerInvariant() switch
    {
        "week" => entry.PlannedDate?.AddDays(7),
        "month" => entry.PlannedDate?.AddMonths(1),
        "year" => entry.PlannedDate?.AddYears(1),
        _ => null
    };
}
