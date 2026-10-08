namespace Nordiska.Modules.Banking.Contracts.Responses;

public sealed record SavingsGoalDepositResponse(
    long SavingsGoalId,
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