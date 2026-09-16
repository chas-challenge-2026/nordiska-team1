using System;

namespace Nordiska.Modules.Banking.Contracts.Responses;

/// <summary>
/// Response model representing a transaction ledger entry.
/// </summary>
public record TransactionResponse(
    long Id,
    long AccountId,
    string Type,
    decimal Amount,
    DateTime CreatedAt,
    string? Label = null,
    long? TargetAccountId = null,
    bool IsPlanned = false,
    DateTime? PlannedDate = null,
    string? Repeating = null
)
{
    public bool Planned => IsPlanned;
    public string? Lable => Label; // Alias matching frontend spec typo
}
