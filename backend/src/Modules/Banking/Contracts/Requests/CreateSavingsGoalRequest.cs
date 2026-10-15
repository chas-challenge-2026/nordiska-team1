using System;

namespace Nordiska.Modules.Banking.Contracts.Requests;

/// <summary>
/// Request to create a new savings goal attached to an account.
/// </summary>
public sealed record CreateSavingsGoalRequest(
    long AccountId,
    string Title,
    decimal TargetAmount,
    DateTime? TargetDate = null
);
