using System.ComponentModel.DataAnnotations;

namespace Nordiska.FrontendApi.Contracts.Requests;

public record UpdateCustomerRequest(
    [property: Required]
    long Id,

    [property: StringLength(200, MinimumLength = 1)]
    string? Name,

    [property: EmailAddress]
    [property: StringLength(320)]
    string? Email,

    [property: StringLength(12, MinimumLength = 10)]
    string? PersonalNum
);

public record CustomerResponse(
    long Id,
    string PersonalNum,
    string Name,
    string Email,
    DateTime CreatedAt
);
