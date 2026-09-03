namespace Nordiska.Modules.Banking.Contracts.Responses;

public record SavingsAccountResponse(
    long Id,
    long CustomerId,
    string AccountNumber,
    string AccountType,
    decimal Balance,
    decimal InterestRate,
    DateTime CreatedAt
);
