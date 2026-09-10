using System;
using Microsoft.AspNetCore.Http;

namespace Nordiska.FrontendApi.Authentication;

/// <summary>
/// Helper extensions for setting and removing HTTP-only authentication cookies.
/// </summary>
public static class AuthCookieExtensions
{
    /// <summary>
    /// The standard cookie name used for JWT authentication.
    /// </summary>
    public const string CookieName = "access_token";

    /// <summary>
    /// Appends a secure, HttpOnly cookie containing the JWT to the response.
    /// </summary>
    /// <param name="response">The current HTTP response.</param>
    /// <param name="token">The generated JWT token string.</param>
    /// <param name="lifetimeInMinutes">Token validity duration in minutes.</param>
    public static void AppendAuthCookie(this HttpResponse response, string token, int lifetimeInMinutes)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = response.HttpContext.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddMinutes(lifetimeInMinutes),
            Path = "/"
        };

        response.Cookies.Append(CookieName, token, cookieOptions);
    }

    /// <summary>
    /// Deletes the authentication cookie from the client browser.
    /// </summary>
    /// <param name="response">The current HTTP response.</param>
    public static void DeleteAuthCookie(this HttpResponse response)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = response.HttpContext.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/"
        };

        response.Cookies.Delete(CookieName, cookieOptions);
    }
}
