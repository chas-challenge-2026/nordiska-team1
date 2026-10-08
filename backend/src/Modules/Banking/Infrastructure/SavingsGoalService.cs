using System;
using System.Collections.Generic;
using System.Linq;
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
    private readonly ISavingsGoalDepositRepository? _depositRepo;
    private readonly IInterestRateService? _interestRateService;

    public SavingsGoalService(
        ISavingsGoalRepository goalRepo,
        ISavingsAccountRepository accountRepo,
        ITransactionRepository txRepo,
        ILogger<SavingsGoalService> logger,
        IInterestRateService? interestRateService = null,
        ISavingsGoalDepositRepository? depositRepo = null)
    {
        _goalRepo = goalRepo;
        _accountRepo = accountRepo;
        _txRepo = txRepo;
        _logger = logger;
        _interestRateService = interestRateService;
        _depositRepo = depositRepo;
    }

    public async Task<SavingsGoalResponse?> GetByIdAsync(
        long goalId,
        long customerId,
        bool isAdmin = false,
        CancellationToken cancellationToken = default)
    {
        var goal = await _goalRepo.GetByIdAsync(goalId, cancellationToken);
        if (goal is null)
        {
            return null;
        }

        if (!isAdmin && goal.CustomerId != customerId)
        {
            return null;
        }

        var rates = await GetInterestRatesAsync(cancellationToken);
        return await MapToResponseAsync(goal, rates, cancellationToken);
    }

    public async Task<List<SavingsGoalResponse>> GetGoalsAsync(
        long customerId,
        long? accountId = null,
        bool isAdmin = false,
        CancellationToken cancellationToken = default)
    {
        List<SavingsGoal> goals;

        if (accountId.HasValue)
        {
            var account = await _accountRepo.GetByIdAsync(accountId.Value, cancellationToken);
            if (account is null || (!isAdmin && account.CustomerId != customerId))
            {
                return new List<SavingsGoalResponse>();
            }

            goals = await _goalRepo.GetByAccountIdAsync(accountId.Value, cancellationToken);
        }
        else
        {
            goals = await _goalRepo.GetByCustomerIdAsync(customerId, cancellationToken);
        }

        var rates = await GetInterestRatesAsync(cancellationToken);
        var responses = new List<SavingsGoalResponse>(goals.Count);
        foreach (var goal in goals)
        {
            responses.Add(await MapToResponseAsync(goal, rates, cancellationToken));
        }

        return responses;
    }

    public async Task<SavingsGoalsOverviewResponse> GetOverviewAsync(
        long customerId,
        CancellationToken cancellationToken = default)
    {
        var goals = await GetGoalsAsync(customerId, cancellationToken: cancellationToken);
        var totalCurrentBalance = goals.Sum(goal => goal.CurrentAmount);
        var totalTargetAmount = goals.Sum(goal => goal.TargetAmount);
        var totalProgress = totalTargetAmount <= 0m
            ? 0m
            : Math.Min(Math.Round(totalCurrentBalance / totalTargetAmount * 100m, 2), 100m);

        return new SavingsGoalsOverviewResponse(
            goals,
            new SavingsGoalsSummaryDto(
                totalCurrentBalance,
                totalTargetAmount,
                totalProgress));
    }

    public async Task<SavingsGoalResponse> CreateAsync(
        CreateSavingsGoalRequest request,
        long customerId,
        bool isAdmin = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 50)
        {
            throw new ValidationException("Namn på sparmål måste anges och får vara max 50 tecken.");
        }

        if (request.TargetAmount <= 0)
        {
            throw new ValidationException("Målbelopp måste vara större än 0 kr.");
        }

        if (request.TargetDate.HasValue && request.TargetDate.Value <= DateTime.UtcNow)
        {
            throw new ValidationException("Måldatum måste vara ett framtida datum.");
        }

        var account = await _accountRepo.GetByIdAsync(request.AccountId, cancellationToken)
            ?? throw new NotFoundException($"Sparkonto med ID {request.AccountId} hittades inte.");

        if (!isAdmin && account.CustomerId != customerId)
        {
            throw new ValidationException("Sparkontot tillhör inte den inloggade kunden.");
        }

        var goal = new SavingsGoal
        {
            AccountId = request.AccountId,
            CustomerId = account.CustomerId,
            Title = request.Title.Trim(),
            TargetAmount = request.TargetAmount,
            CurrentAmount = 0m,
            TargetDate = request.TargetDate,
            Status = "active",
            CreatedAt = DateTime.UtcNow
        };

        var id = await _goalRepo.CreateAsync(goal, cancellationToken);
        goal.Id = id;

        _logger.LogInformation("Created savings goal {GoalId} '{Title}' for customer {CustomerId} on account {AccountId}",
            goal.Id, goal.Title, goal.CustomerId, goal.AccountId);

        var rates = await GetInterestRatesAsync(cancellationToken);
        return await MapToResponseAsync(goal, rates, cancellationToken);
    }

    public async Task<SavingsGoalResponse> UpdateAsync(
        long goalId,
        UpdateSavingsGoalRequest request,
        long customerId,
        bool isAdmin = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var goal = await _goalRepo.GetByIdAsync(goalId, cancellationToken)
            ?? throw new NotFoundException($"Sparmål med ID {goalId} hittades inte.");

        if (!isAdmin && goal.CustomerId != customerId)
        {
            throw new NotFoundException($"Sparmål med ID {goalId} hittades inte.");
        }

        if (request.Title is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 50)
            {
                throw new ValidationException("Namn på sparmål får inte vara tomt och kan max vara 50 tecken.");
            }
            goal.Title = request.Title.Trim();
        }

        if (request.TargetAmount.HasValue)
        {
            if (request.TargetAmount.Value <= 0)
            {
                throw new ValidationException("Målbelopp måste vara större än 0 kr.");
            }
            goal.TargetAmount = request.TargetAmount.Value;
        }

        if (request.TargetDate.HasValue)
        {
            if (request.TargetDate.Value <= DateTime.UtcNow)
            {
                throw new ValidationException("Måldatum måste vara ett framtida datum.");
            }
            goal.TargetDate = request.TargetDate.Value;
        }

        if (request.Status is not null)
        {
            var status = request.Status.Trim().ToLowerInvariant();
            if (status is not ("active" or "paused" or "completed"))
            {
                throw new ValidationException("Ogiltig status. Tillåtna statusar är: active, paused, completed.");
            }
            goal.Status = status;
        }

        goal.UpdatedAt = DateTime.UtcNow;

        await _goalRepo.UpdateAsync(goal, cancellationToken);
        _logger.LogInformation("Updated savings goal {GoalId}", goal.Id);

        var rates = await GetInterestRatesAsync(cancellationToken);
        return await MapToResponseAsync(goal, rates, cancellationToken);
    }

    public async Task<bool> DeleteAsync(
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

        // Clean up any automated monthly recurring plan tied to this goal
        var existingPlan = await _txRepo.GetPlannedTransactionByGoalIdAsync(goalId, cancellationToken);
        if (existingPlan is not null)
        {
            await _txRepo.DeleteAsync(existingPlan.Id, cancellationToken);
            _logger.LogInformation("Cancelled planned recurring transaction {TxId} during goal {GoalId} deletion", existingPlan.Id, goalId);
        }

        await _goalRepo.DeleteAsync(goalId, cancellationToken);
        _logger.LogInformation("Deleted savings goal {GoalId} for customer {CustomerId}", goalId, customerId);

        return true;
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

    public async Task<SavingsGoalDepositResponse> DepositAsync(
        long goalId,
        DepositToSavingsGoalRequest request,
        long customerId,
        bool isAdmin = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Amount <= 0)
        {
            throw new ValidationException("Insättningsbeloppet måste vara större än 0 kr.");
        }

        var depositRepo = _depositRepo
            ?? throw new InvalidOperationException("Savings goal deposit repository is not registered.");

        var result = await depositRepo.DepositAsync(
            goalId,
            request.SourceAccountId,
            request.Amount,
            customerId,
            isAdmin,
            cancellationToken);

        _logger.LogInformation(
            "Deposited {Amount} from account {SourceAccountId} to savings goal {SavingsGoalId}. CompletedNow={CompletedNow}",
            result.Amount,
            result.SourceAccountId,
            result.SavingsGoalId,
            result.CompletedNow);

        return new SavingsGoalDepositResponse(
            result.SavingsGoalId,
            result.GoalTitle,
            result.SourceAccountId,
            result.TargetAccountId,
            result.Amount,
            result.SourceAccountBalance,
            result.TargetAccountBalance,
            result.CurrentAmount,
            result.TargetAmount,
            result.Status,
            result.CompletedNow,
            result.DepositedAt);
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

    private async Task<IReadOnlyDictionary<string, decimal>> GetInterestRatesAsync(
        CancellationToken cancellationToken)
    {
        if (_interestRateService is null)
        {
            return new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        }

        var rates = await _interestRateService.GetAllAsync(cancellationToken);
        return rates.ToDictionary(
            rate => rate.AccountType,
            rate => rate.InterestRate,
            StringComparer.OrdinalIgnoreCase);
    }

    private async Task<SavingsGoalResponse> MapToResponseAsync(
        SavingsGoal goal,
        IReadOnlyDictionary<string, decimal> interestRates,
        CancellationToken cancellationToken)
    {
        var account = goal.Account
            ?? await _accountRepo.GetByIdAsync(goal.AccountId, cancellationToken);
        var annualInterestRate = account is not null
            && interestRates.TryGetValue(account.AccountType, out var configuredRate)
                ? configuredRate
                : account?.InterestRate ?? 0m;

        var plannedTransfer = await _txRepo.GetPlannedTransactionByGoalIdAsync(
            goal.Id,
            cancellationToken);
        var monthlyContribution = plannedTransfer is null
            ? 0m
            : Math.Abs(plannedTransfer.Amount);

        var progress = goal.TargetAmount > 0m
            ? Math.Min(Math.Round(goal.CurrentAmount / goal.TargetAmount * 100m, 2), 100m)
            : 0m;
        var (etaLabel, estimatedCompletionDate) = CalculateEta(
            goal.CurrentAmount,
            goal.TargetAmount,
            annualInterestRate,
            monthlyContribution,
            DateTime.UtcNow);

        return new SavingsGoalResponse(
            Id: goal.Id,
            AccountId: goal.AccountId,
            CustomerId: goal.CustomerId,
            Title: goal.Title,
            TargetAmount: goal.TargetAmount,
            CurrentAmount: goal.CurrentAmount,
            TargetDate: goal.TargetDate,
            Status: goal.Status,
            ProgressPercentage: progress,
            CreatedAt: goal.CreatedAt,
            UpdatedAt: goal.UpdatedAt,
            EtaLabel: etaLabel,
            EstimatedCompletionDate: estimatedCompletionDate);
    }

    private static (string EtaLabel, DateTimeOffset? CompletionDate) CalculateEta(
        decimal currentAmount,
        decimal targetAmount,
        decimal annualInterestRate,
        decimal monthlyContribution,
        DateTime asOfUtc)
    {
        if (targetAmount <= 0m || currentAmount >= targetAmount)
        {
            return ("Completed", new DateTimeOffset(DateTime.SpecifyKind(asOfUtc, DateTimeKind.Utc)));
        }

        if (annualInterestRate <= -1m)
        {
            return ("No ETA", null);
        }

        var monthlyRate = (decimal)Math.Pow(
            (double)(1m + annualInterestRate),
            1d / 12d) - 1m;
        var projectedAmount = currentAmount;

        for (var month = 1; month <= 1_200; month++)
        {
            projectedAmount = decimal.Round(
                projectedAmount * (1m + monthlyRate),
                2,
                MidpointRounding.ToEven);
            projectedAmount += monthlyContribution;

            if (projectedAmount >= targetAmount)
            {
                var label = month == 1
                    ? "Approx. 1 month left"
                    : $"Approx. {month} months left";
                return (label, new DateTimeOffset(DateTime.SpecifyKind(asOfUtc, DateTimeKind.Utc)).AddMonths(month));
            }
        }

        return ("No ETA", null);
    }
}
