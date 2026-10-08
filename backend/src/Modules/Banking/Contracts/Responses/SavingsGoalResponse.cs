using System;

namespace Nordiska.Modules.Banking.Contracts.Responses;

/// <summary>
/// Response representing a savings goal.
/// </summary>
public sealed record SavingsGoalResponse(
    long Id,
    long AccountId,
    long CustomerId,
    string Title,
    decimal TargetAmount,
    decimal CurrentAmount,
    DateTime? TargetDate,
    string Status,
    decimal ProgressPercentage,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string? EtaLabel = null,
    DateTimeOffset? EstimatedCompletionDate = null
);
