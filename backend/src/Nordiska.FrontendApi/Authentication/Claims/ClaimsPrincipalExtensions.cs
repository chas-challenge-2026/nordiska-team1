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
    /// Returns the customer id from the token, or null if it is missing or not a number.
    /// </summary>
    public static long? GetCustomerId(this ClaimsPrincipal user)
    {
        // The JWT handler maps "sub" to NameIdentifier by default, so check both
        var value = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
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
