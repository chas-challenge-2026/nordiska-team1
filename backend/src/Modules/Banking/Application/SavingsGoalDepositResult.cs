namespace Nordiska.Modules.Banking.Application;

public sealed record SavingsGoalDepositResult(
    long SavingsGoalId,
    long CustomerId,
    string GoalTitle,
    long SourceAccountId,
    long TargetAccountId,
    decimal Amount,
    decimal SourceAccountBalance,
    decimal TargetAccountBalance,
    decimal CurrentAmount,
    decimal TargetAmount,
    string Status,
    bool CompletedNow,
    DateTime DepositedAt
);