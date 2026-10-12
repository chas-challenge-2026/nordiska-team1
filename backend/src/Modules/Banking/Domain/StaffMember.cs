using System;

namespace Nordiska.Modules.Banking.Domain;

/// <summary>
/// Internal administrative or banking staff member entity (RBAC).
/// Separated from regular retail customers.
/// </summary>
public sealed class StaffMember
{
    public long Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = "Admin";
    public string? PhoneNumber { get; set; }
    public string? Department { get; set; }
    public string? EmployeeNumber { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}