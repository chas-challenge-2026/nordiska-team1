using System;
using System.Collections.Generic;

namespace Nordiska.FrontendApi.Contracts.Responses;

/// <summary>
/// Detailed response model for administrative and staff users.
/// </summary>
public sealed record AdminUserResponse(
    long Id,
    string Email,
    string Name,
    string? PhoneNumber,
    string? PersonalNum,
    IReadOnlyList<string> Roles,
    DateTime CreatedAt,
    bool IsLockedOut);