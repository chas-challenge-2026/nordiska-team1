using System.ComponentModel.DataAnnotations;

namespace Nordiska.Modules.Banking.Contracts.Requests;

public record OpenSavingsAccountRequest(
    [property: Required]
    long CustomerId,

    [property: Required]
    [property: StringLength(34, MinimumLength = 4)]
    string AccountNumber,

    [property: Required]
    [property: StringLength(100)]
    string AccountType,

    [property: Range(0, 1_000_000_000)]
    decimal InitialDeposit,

    [property: Range(0.0, 100.0)]
    decimal InterestRate
);

public record UpdateSavingsAccountRequest(
    [property: Required]
    long Id,

    [property: StringLength(100)]
    string? AccountType,

    [property: Range(0, 1_000_000_000)]
    decimal? Balance,

    [property: Range(0.0, 100.0)]
    decimal? InterestRate
);

public record CloseSavingsAccountRequest(
    [property: Required]
    long Id,

    [property: Required]
    string Reason
);
