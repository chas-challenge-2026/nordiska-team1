namespace Nordiska.Modules.Banking.Contracts.Responses;

public record AccountTypeConfigResponse(
    string AccountType,
    decimal InterestRate,
    string Description
);

public record AccountTypeRateHistoryResponse(
    long Id,
    string AccountType,
    decimal InterestRate,
    System.DateTime EffectiveFromUtc,
    System.DateTime? EffectiveToUtc
);
