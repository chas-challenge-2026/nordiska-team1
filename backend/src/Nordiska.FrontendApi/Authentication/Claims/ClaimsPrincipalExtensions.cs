using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Nordiska.FrontendApi.Authentication.Claims;

/// <summary>
/// Helpers for reading the logged in customer from the JWT claims.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    private const string AdminRole = "Admin";

    /// <summary>
    /// True if the authenticated user is an internal staff or admin member.
    /// </summary>
    public static bool IsStaff(this ClaimsPrincipal user)
    {
        return user.HasClaim(c => c.Type == "staff_id")
               || string.Equals(user.FindFirst("user_type")?.Value, "staff", StringComparison.OrdinalIgnoreCase)
               || user.IsInRole(AdminRole) || user.IsInRole("Staff") || user.IsInRole("BankStaff");
    }

    /// <summary>
    /// Returns the customer id from the token, or null if it is missing, not a number, or if the user is staff.
    /// </summary>
    public static long? GetCustomerId(this ClaimsPrincipal user)
    {
        // Staff/Admin users have their own staff_id and do not represent a customer.
        // Explicitly prevent staff members (e.g. staff.Id == 1) from colliding with Customer #1.
        if (user.IsStaff())
        {
            return null;
        }

        var value = user.FindFirst("customer_id")?.Value
                    ?? user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                    ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return long.TryParse(value, out var customerId) ? customerId : null;
    }

    /// <summary>
    /// Returns the customer id from the token. Throws if the token has no valid customer id,
    /// which the global exception handler turns into 401.
    /// </summary>
    public static long GetRequiredCustomerId(this ClaimsPrincipal user)
    {
        return user.GetCustomerId()
               ?? throw new UnauthorizedAccessException("Token saknar giltigt kund-id.");
    }

    /// <summary>
    /// Returns the staff id from the token claims ("staff_id" or "sub" when user_type is staff), or null if missing or not a number.
    /// </summary>
    public static long? GetStaffId(this ClaimsPrincipal user)
    {
        var value = user.FindFirst("staff_id")?.Value;
        if (!string.IsNullOrEmpty(value) && long.TryParse(value, out var staffId))
        {
            return staffId;
        }

        if (user.IsStaff())
        {
            var sub = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                      ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (long.TryParse(sub, out var id))
            {
                return id;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the staff id from the token. Throws if the token has no valid staff id,
    /// which the global exception handler turns into 401.
    /// </summary>
    public static long GetRequiredStaffId(this ClaimsPrincipal user)
    {
        return user.GetStaffId()
               ?? throw new UnauthorizedAccessException("Token saknar giltigt handläggar-id.");
    }

    /// <summary>
    /// True if the logged in user has the Admin role.
    /// </summary>
    public static bool IsAdmin(this ClaimsPrincipal user) => user.IsInRole(AdminRole);

    /// <summary>
    /// True if the logged in user is the given customer, or an admin.
    /// </summary>
    public static bool CanAccessCustomer(this ClaimsPrincipal user, long customerId)
    {
        return user.IsAdmin() || user.GetCustomerId() == customerId;
    }
}
