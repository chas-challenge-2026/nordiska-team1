namespace Nordiska.Modules.Banking.Contracts.Responses;

public sealed record SavingsGoalsOverviewResponse(
    IReadOnlyList<SavingsGoalResponse> Goals,
    SavingsGoalsSummaryDto Summary);

public sealed record SavingsGoalsSummaryDto(
    decimal TotalCurrentBalance,
    decimal TotalTargetAmount,
    decimal TotalProgressPercentage);