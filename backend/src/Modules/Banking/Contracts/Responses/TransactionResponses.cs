namespace Nordiska.Modules.Banking.Contracts.Responses;

/// <summary>
/// Response model representing a transaction.
/// </summary>
public record TransactionResponse(
    long Id,
    long AccountId,
    string Type,
    decimal Amount,
    DateTime CreatedAt
);
