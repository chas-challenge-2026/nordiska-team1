namespace Nordiska.FrontendApi.Contracts.Requests;

/// <summary>
/// Request payload for creating a new administrator or bank staff user.
/// </summary>
public sealed record CreateAdminUserRequest
{
    /// <summary>
    /// Email address for login. Must be unique.
    /// </summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Full display name of the user.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Initial password for login (minimum 6 characters).
    /// </summary>
    public string Password { get; init; } = string.Empty;

    /// <summary>
    /// Role to assign: "Admin", "BankStaff", or "Staff". Defaults to "Admin".
    /// </summary>
    public string Role { get; init; } = "Admin";

    /// <summary>
    /// Optional contact phone number.
    /// </summary>
    public string? PhoneNumber { get; init; }

    /// <summary>
    /// Optional 12-digit personal identity number. If omitted, an internal administrative identifier will be generated.
    /// </summary>
    public string? PersonalNum { get; init; }
}

/// <summary>
/// Request payload for updating administrative user properties.
/// </summary>
public sealed record UpdateAdminUserRequest
{
    /// <summary>
    /// Updated display name.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Updated phone number.
    /// </summary>
    public string? PhoneNumber { get; init; }

    /// <summary>
    /// Updated role: "Admin", "BankStaff", or "Staff".
    /// </summary>
    public string? Role { get; init; }
}

/// <summary>
/// Request payload for updating an administrator's password.
/// </summary>
public sealed record ChangeAdminPasswordRequest
{
    /// <summary>
    /// New password (minimum 6 characters).
    /// </summary>
    public string NewPassword { get; init; } = string.Empty;
}