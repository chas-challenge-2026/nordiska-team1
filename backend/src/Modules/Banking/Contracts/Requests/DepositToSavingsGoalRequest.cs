namespace Nordiska.Modules.Banking.Contracts.Requests;

public sealed record DepositToSavingsGoalRequest(
    long SourceAccountId,
    decimal Amount);
