using System.ComponentModel.DataAnnotations;

namespace Nordiska.Modules.Banking.Contracts.Requests;

public record CreateAccountTypeConfigRequest(
    [property: Required]
    [property: StringLength(100, MinimumLength = 1)]
    string AccountType,

    [property: Range(0.0, 100.0)]
    decimal InterestRate,

    [property: StringLength(1000)]
    string? Description
);

public record UpdateAccountTypeConfigRequest(
    [property: Required]
    [property: StringLength(100, MinimumLength = 1)]
    string AccountType,

    [property: Range(0.0, 100.0)]
    decimal? InterestRate,

    [property: StringLength(1000)]
    string? Description
);
