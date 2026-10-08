using System.Data;
using Microsoft.EntityFrameworkCore;
using Nordiska.BuildingBlocks.Database.Errors;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Domain;
using Nordiska.Modules.Banking.Infrastructure.Db;

namespace Nordiska.Modules.Banking.Infrastructure;

public sealed class SavingsGoalDepositRepository(
    BankingDbContext db) : ISavingsGoalDepositRepository
{
    public async Task<SavingsGoalDepositResult> DepositAsync(
        long savingsGoalId,
        long sourceAccountId,
        decimal amount,
        long customerId,
        bool isAdmin = false,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
        {
            throw new ValidationException(
                "Insättningsbeloppet måste vara större än 0 kr.");
        }

        await using var transaction =
            await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var lockedGoals = await db.SavingsGoals
            .FromSqlInterpolated($"""
                SELECT *
                FROM banking.savings_goals
                WHERE "Id" = {savingsGoalId}
                FOR UPDATE
                """)
            .ToListAsync(cancellationToken);

        var goal = lockedGoals.SingleOrDefault()
            ?? throw new NotFoundException(
                $"Sparmål med ID {savingsGoalId} hittades inte.");

        if (!isAdmin && goal.CustomerId != customerId)
        {
            throw new NotFoundException(
                $"Sparmål med ID {savingsGoalId} hittades inte.");
        }

        if (string.Equals(
                goal.Status,
                "completed",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException(
                "Det går inte att göra en insättning till ett slutfört sparmål.");
        }

        if (sourceAccountId == goal.AccountId)
        {
            throw new ValidationException(
                "Källkontot kan inte vara samma konto som sparmålets konto.");
        }

        var firstAccountId = Math.Min(sourceAccountId, goal.AccountId);
        var secondAccountId = Math.Max(sourceAccountId, goal.AccountId);

        var lockedAccounts = await db.SavingsAccounts
            .FromSqlInterpolated($"""
                SELECT *
                FROM banking.savings_accounts
                WHERE "Id" IN ({firstAccountId}, {secondAccountId})
                ORDER BY "Id"
                FOR UPDATE
                """)
            .ToListAsync(cancellationToken);

        var sourceAccount = lockedAccounts
            .SingleOrDefault(account => account.Id == sourceAccountId)
            ?? throw new NotFoundException(
                $"Källkonto med ID {sourceAccountId} hittades inte.");

        var targetAccount = lockedAccounts
            .SingleOrDefault(account => account.Id == goal.AccountId)
            ?? throw new NotFoundException(
                $"Sparmålets konto med ID {goal.AccountId} hittades inte.");

        if (!isAdmin && sourceAccount.CustomerId != customerId)
        {
            throw new ValidationException(
                "Källkontot tillhör inte den inloggade kunden.");
        }

        if (!string.Equals(
                sourceAccount.Status,
                "active",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException("Källkontot är inte aktivt.");
        }

        if (!string.Equals(
                targetAccount.Status,
                "active",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException("Sparmålets konto är inte aktivt.");
        }

        if (!string.Equals(
                sourceAccount.CurrencyCode,
                targetAccount.CurrencyCode,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException(
                "Överföring mellan konton med olika valutor stöds inte.");
        }

        var sourceBalance = await GetLedgerBalanceAsync(
            sourceAccount.Id,
            cancellationToken);

        if (sourceBalance < amount)
        {
            throw new ConflictException(
                $"Otillräckligt saldo. Tillgängligt saldo är " +
                $"{sourceBalance:N2} kr och insättningen är {amount:N2} kr.");
        }

        var targetBalance = await GetLedgerBalanceAsync(
            targetAccount.Id,
            cancellationToken);

        var depositedAt = DateTime.UtcNow;
        var label = $"Engångsinsättning till sparmål: {goal.Title}";

        var withdrawal = new LedgerEntry
        {
            AccountId = sourceAccount.Id,
            Type = "transfer",
            Amount = -amount,
            Label = label,
            TargetAccountId = targetAccount.Id,
            CreatedAt = depositedAt
        };

        var deposit = new LedgerEntry
        {
            AccountId = targetAccount.Id,
            Type = "transfer",
            Amount = amount,
            Label = label,
            TargetAccountId = sourceAccount.Id,
            SavingsGoalId = goal.Id,
            CreatedAt = depositedAt
        };

        db.LedgerEntries.AddRange(withdrawal, deposit);

        sourceAccount.Balance = sourceBalance - amount;
        sourceAccount.UpdatedAt = depositedAt;

        targetAccount.Balance = targetBalance + amount;
        targetAccount.UpdatedAt = depositedAt;

        goal.CurrentAmount += amount;
        goal.UpdatedAt = depositedAt;

        var completedNow =
            goal.CurrentAmount >= goal.TargetAmount &&
            !string.Equals(
                goal.Status,
                "completed",
                StringComparison.OrdinalIgnoreCase);

        if (completedNow)
        {
            goal.Status = "completed";
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new SavingsGoalDepositResult(
            SavingsGoalId: goal.Id,
            CustomerId: goal.CustomerId,
            GoalTitle: goal.Title,
            SourceAccountId: sourceAccount.Id,
            TargetAccountId: targetAccount.Id,
            Amount: amount,
            SourceAccountBalance: sourceAccount.Balance,
            TargetAccountBalance: targetAccount.Balance,
            CurrentAmount: goal.CurrentAmount,
            TargetAmount: goal.TargetAmount,
            Status: goal.Status,
            CompletedNow: completedNow,
            DepositedAt: depositedAt);
    }

    private async Task<decimal> GetLedgerBalanceAsync(
        long accountId,
        CancellationToken cancellationToken)
    {
        return await db.LedgerEntries
            .Where(entry =>
                entry.AccountId == accountId &&
                !entry.IsPlanned)
            .SumAsync(
                entry => (decimal?)entry.Amount,
                cancellationToken)
            ?? 0m;
    }
}