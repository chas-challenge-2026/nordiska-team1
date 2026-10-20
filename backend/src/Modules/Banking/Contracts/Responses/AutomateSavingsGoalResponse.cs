using System;

namespace Nordiska.Modules.Banking.Contracts.Responses;

/// <summary>
/// Details of an automated recurring monthly savings transfer.
/// </summary>
public sealed record AutomateSavingsGoalResponse(
    long SavingsGoalId,
    long SourceAccountId,
    long TargetAccountId,
    decimal MonthlyAmount,
    int DayOfMonth,
    DateTime NextExecutionDate,
    string Status
);
