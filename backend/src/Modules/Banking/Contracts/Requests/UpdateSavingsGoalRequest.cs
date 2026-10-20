using System;

namespace Nordiska.Modules.Banking.Contracts.Requests;

/// <summary>
/// Request to update an existing savings goal.
/// </summary>
public sealed record UpdateSavingsGoalRequest(
    string? Title = null,
    decimal? TargetAmount = null,
    DateTime? TargetDate = null,
    string? Status = null
);
