namespace Nordiska.FrontendApi.Authentication.Jwt;

public class JwtOptions
{
    /// <summary>
    /// Secret key used to sign JWT tokens. Keep this secure.
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;
    /// <summary>
    /// Token issuer identifier.
    /// </summary>
    public string Issuer { get; set; } = string.Empty;
    /// <summary>
    /// Token audience identifier.
    /// </summary>
    public string Audience { get; set; } = string.Empty;
    /// <summary>
    /// Lifetime of the token in minutes.
    /// </summary>
    public int TokenLifetimeInMinutes { get; set; }
}
