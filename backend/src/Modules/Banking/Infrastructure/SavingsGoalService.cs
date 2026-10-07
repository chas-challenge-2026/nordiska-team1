using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nordiska.BuildingBlocks.Database.Errors;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Contracts.Responses;
using Nordiska.Modules.Banking.Domain;

namespace Nordiska.Modules.Banking.Infrastructure;

public sealed class SavingsGoalService : ISavingsGoalService
{
    private readonly ISavingsGoalRepository _goalRepo;
    private readonly ISavingsAccountRepository _accountRepo;
    private readonly ITransactionRepository _txRepo;
    private readonly ILogger<SavingsGoalService> _logger;

    public SavingsGoalService(
        ISavingsGoalRepository goalRepo,
        ISavingsAccountRepository accountRepo,
        ITransactionRepository txRepo,
        ILogger<SavingsGoalService> logger)
    {
        _goalRepo = goalRepo;
        _accountRepo = accountRepo;
        _txRepo = txRepo;
        _logger = logger;
    }

    public async Task<SavingsGoal?> GetByIdAsync(long goalId, CancellationToken cancellationToken = default)
    {
        return await _goalRepo.GetByIdAsync(goalId, cancellationToken);
    }

    public async Task<AutomateSavingsGoalResponse> AutomateAsync(
        long goalId,
        AutomateSavingsGoalRequest request,
        long customerId,
        bool isAdmin = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.MonthlyAmount <= 0)
        {
            throw new ValidationException("Månadsbelopp måste vara större än 0.");
        }

        if (request.DayOfMonth is < 1 or > 31)
        {
            throw new ValidationException("Dag i månaden måste vara mellan 1 och 31.");
        }

        var goal = await _goalRepo.GetByIdAsync(goalId, cancellationToken)
            ?? throw new NotFoundException($"Sparmål med ID {goalId} hittades inte.");

        if (!isAdmin && goal.CustomerId != customerId)
        {
            throw new NotFoundException($"Sparmål med ID {goalId} hittades inte.");
        }

        var sourceAccount = await _accountRepo.GetByIdAsync(request.SourceAccountId, cancellationToken)
            ?? throw new ValidationException($"Källkonto med ID {request.SourceAccountId} hittades inte.");

        if (!isAdmin && sourceAccount.CustomerId != customerId)
        {
            throw new ValidationException($"Källkontot tillhör inte den inloggade kunden.");
        }

        if (request.SourceAccountId == goal.AccountId)
        {
            throw new ValidationException("Källkontot kan inte vara samma som sparmålets sparkonto.");
        }

        var nextExecutionDate = CalculateNextExecutionDate(request.DayOfMonth, DateTime.UtcNow);

        var existingPlan = await _txRepo.GetPlannedTransactionByGoalIdAsync(goalId, cancellationToken);
        if (existingPlan is not null)
        {
            existingPlan.AccountId = request.SourceAccountId;
            existingPlan.TargetAccountId = goal.AccountId;
            existingPlan.Amount = request.MonthlyAmount;
            existingPlan.PlannedDate = nextExecutionDate;
            existingPlan.Repeating = "month";
            existingPlan.Label = $"Månadssparande: {goal.Title}";

            await _txRepo.UpdateAsync(existingPlan, cancellationToken);
            _logger.LogInformation("Updated monthly automated transfer for savings goal {GoalId} to {Amount} SEK on day {Day}",
                goal.Id, request.MonthlyAmount, request.DayOfMonth);
        }
        else
        {
            var plan = new LedgerEntry
            {
                AccountId = request.SourceAccountId,
                TargetAccountId = goal.AccountId,
                Type = "transfer",
                Amount = request.MonthlyAmount,
                Label = $"Månadssparande: {goal.Title}",
                IsPlanned = true,
                PlannedDate = nextExecutionDate,
                Repeating = "month",
                SavingsGoalId = goal.Id,
                CreatedAt = DateTime.UtcNow
            };

            await _txRepo.CreateAsync(plan, cancellationToken);
            _logger.LogInformation("Created monthly automated transfer for savings goal {GoalId} with {Amount} SEK on day {Day}",
                goal.Id, request.MonthlyAmount, request.DayOfMonth);
        }

        return new AutomateSavingsGoalResponse(
            SavingsGoalId: goal.Id,
            SourceAccountId: request.SourceAccountId,
            TargetAccountId: goal.AccountId,
            MonthlyAmount: request.MonthlyAmount,
            DayOfMonth: request.DayOfMonth,
            NextExecutionDate: nextExecutionDate,
            Status: goal.Status
        );
    }

    public async Task<bool> CancelAutomationAsync(
        long goalId,
        long customerId,
        bool isAdmin = false,
        CancellationToken cancellationToken = default)
    {
        var goal = await _goalRepo.GetByIdAsync(goalId, cancellationToken)
            ?? throw new NotFoundException($"Sparmål med ID {goalId} hittades inte.");

        if (!isAdmin && goal.CustomerId != customerId)
        {
            throw new NotFoundException($"Sparmål med ID {goalId} hittades inte.");
        }

        var existingPlan = await _txRepo.GetPlannedTransactionByGoalIdAsync(goalId, cancellationToken);
        if (existingPlan is not null)
        {
            await _txRepo.DeleteAsync(existingPlan.Id, cancellationToken);
            _logger.LogInformation("Cancelled automated transfer for savings goal {GoalId}", goalId);
        }

        return true;
    }

    public static DateTime CalculateNextExecutionDate(int dayOfMonth, DateTime asOfUtc)
    {
        if (dayOfMonth is < 1 or > 31)
        {
            throw new ArgumentOutOfRangeException(nameof(dayOfMonth), "Day of month must be between 1 and 31.");
        }

        var year = asOfUtc.Year;
        var month = asOfUtc.Month;
        var daysInCurrentMonth = DateTime.DaysInMonth(year, month);
        var targetDayThisMonth = Math.Min(dayOfMonth, daysInCurrentMonth);

        if (targetDayThisMonth > asOfUtc.Day)
        {
            return new DateTime(year, month, targetDayThisMonth, 0, 0, 0, DateTimeKind.Utc);
        }

        var nextMonthDate = asOfUtc.AddMonths(1);
        var daysInNextMonth = DateTime.DaysInMonth(nextMonthDate.Year, nextMonthDate.Month);
        var targetDayNextMonth = Math.Min(dayOfMonth, daysInNextMonth);

        return new DateTime(nextMonthDate.Year, nextMonthDate.Month, targetDayNextMonth, 0, 0, 0, DateTimeKind.Utc);
    }
}
