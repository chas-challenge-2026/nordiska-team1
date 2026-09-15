using System.ComponentModel.DataAnnotations;

namespace Nordiska.FrontendApi.Contracts.Requests;

/// <summary>
/// Request payload for updating customer profile information.
/// </summary>
/// <param name="Id">The unique customer identifier.</param>
/// <param name="Name">The updated customer name.</param>
/// <param name="Email">The updated email address.</param>
/// <param name="PersonalNum">The Swedish personal identification number (10-12 digits).</param>
/// <param name="PhoneNumber">The optional updated phone number.</param>
public record UpdateCustomerRequest(
    [property: Required]
    long Id,

    [property: StringLength(200, MinimumLength = 1)]
    string? Name,

    [property: EmailAddress]
    [property: StringLength(320)]
    string? Email,

    [property: StringLength(12, MinimumLength = 10)]
    string? PersonalNum,

    [property: Phone]
    [property: StringLength(50)]
    string? PhoneNumber = null
);

/// <summary>
/// Customer profile response representation.
/// </summary>
/// <param name="Id">Unique identifier of the customer.</param>
/// <param name="PersonalNum">Customer's personal identification number.</param>
/// <param name="Name">Full name of the customer.</param>
/// <param name="Email">Customer email address.</param>
/// <param name="PhoneNumber">Optional contact phone number.</param>
/// <param name="CreatedAt">Timestamp when the customer profile was created.</param>
public record CustomerResponse(
    long Id,
    string PersonalNum,
    string Name,
    string Email,
    string? PhoneNumber,
    DateTime CreatedAt
);
