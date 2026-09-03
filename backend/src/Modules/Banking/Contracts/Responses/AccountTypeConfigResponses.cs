namespace Nordiska.Modules.Banking.Contracts.Responses;

public record AccountTypeConfigResponse(
    string AccountType,
    decimal InterestRate,
    string Description
);
