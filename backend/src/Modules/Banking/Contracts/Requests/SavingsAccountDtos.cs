namespace Nordiska.Modules.Banking.Contracts.Requests;

public record OpenSavingsAccountRequest(
    long CustomerId,
    string AccountNumber,
    string AccountType,
    decimal InitialDeposit,
    decimal InterestRate
);

public record UpdateSavingsAccountRequest(
    long Id,
    string? AccountType,
    decimal? Balance,
    decimal? InterestRate
);

public record CloseSavingsAccountRequest(
    long Id,
    string Reason
);
