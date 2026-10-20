namespace Nordiska.Modules.Banking.Contracts.Requests;

/// <summary>
/// Request to setup or update automated recurring monthly transfer towards a savings goal.
/// </summary>
public sealed record AutomateSavingsGoalRequest(
    long SourceAccountId,
    decimal MonthlyAmount,
    int DayOfMonth
);
