using System;
using System.Collections.Generic;

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
    string? Repeating = null,
    Guid? CorrelationId = null
)
{
    public bool Planned => IsPlanned;
}

/// <summary>
/// A grouped collection of transactions for a specific calendar date (formatted as yyyy-MM-dd).
/// </summary>
public record TransactionDateGroup(
    string Date,
    IReadOnlyList<TransactionResponse> Items
)
{
    /// <summary>
    /// Alias matching frontend dateKey convention.
    /// </summary>
    public string DateKey => Date;
}

/// <summary>
/// Paginated query result with both date groups and flat items.
/// </summary>
public record GroupedTransactionsPagedResult(
    IReadOnlyList<TransactionDateGroup> Groups,
    IReadOnlyCollection<TransactionResponse> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage
);