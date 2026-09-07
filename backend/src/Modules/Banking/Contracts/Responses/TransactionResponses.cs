namespace Nordiska.Modules.Banking.Contracts.Responses;

public record TransactionResponse(
    long Id,
    long AccountId,
    string Type,
    decimal Amount,
    DateTime CreatedAt
);
