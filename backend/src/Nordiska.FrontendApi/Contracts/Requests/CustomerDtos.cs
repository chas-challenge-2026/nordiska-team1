using System;
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
/// <param name="Phone">Alias for phone number.</param>
/// <param name="OverviewPreference">The optional updated list of overview card preference keys.</param>
public record UpdateCustomerRequest(
    [Required]
    long Id,

    [StringLength(200, MinimumLength = 1)]
    string? Name = null,

    [EmailAddress]
    [StringLength(320)]
    string? Email = null,

    [StringLength(12, MinimumLength = 10)]
    string? PersonalNum = null,

    [Phone]
    [StringLength(50)]
    string? PhoneNumber = null,

    [Phone]
    [StringLength(50)]
    string? Phone = null,

    List<string>? OverviewPreference = null
)
{
    public string? EffectivePhone => PhoneNumber ?? Phone;
}

/// <summary>
/// Request payload for partially patching customer profile fields.
/// </summary>
/// <param name="Id">Optional customer identifier.</param>
/// <param name="Name">Optional updated name.</param>
/// <param name="Email">Optional updated email address.</param>
/// <param name="PhoneNumber">Optional updated phone number.</param>
/// <param name="Phone">Alias for phone number.</param>
/// <param name="OverviewPreference">Optional updated list of overview card preference keys.</param>
public record PatchCustomerRequest(
    long? Id = null,

    [StringLength(200, MinimumLength = 1)]
    string? Name = null,

    [EmailAddress]
    [StringLength(320)]
    string? Email = null,

    [Phone]
    [StringLength(50)]
    string? PhoneNumber = null,

    [Phone]
    [StringLength(50)]
    string? Phone = null,

    List<string>? OverviewPreference = null
)
{
    public string? EffectivePhone => PhoneNumber ?? Phone;
}

/// <summary>
/// Customer profile response representation.
/// </summary>
/// <param name="Id">Unique identifier of the customer.</param>
/// <param name="PersonalNum">Customer's personal identification number.</param>
/// <param name="Name">Full name of the customer.</param>
/// <param name="Email">Customer email address.</param>
/// <param name="PhoneNumber">Optional contact phone number.</param>
/// <param name="Phone">Alias for phone number.</param>
/// <param name="CreatedAt">Timestamp when the customer profile was created.</param>
/// <param name="UpdatedAt">Optional timestamp when the customer profile was last updated.</param>
/// <param name="OverviewPreference">List of overview card preference keys in desired display order.</param>
public record CustomerResponse(
    long Id,
    string PersonalNum,
    string Name,
    string Email,
    string? PhoneNumber,
    DateTime CreatedAt,
    string? Phone = null,
    DateTime? UpdatedAt = null,
    IReadOnlyList<string>? OverviewPreference = null
);
