namespace Nordiska.Modules.Banking.Contracts.Requests;

public record CreateAccountTypeConfigRequest(
    string AccountType,
    decimal InterestRate,
    string? Description
);

public record UpdateAccountTypeConfigRequest(
    string AccountType,
    decimal? InterestRate,
    string? Description
);
