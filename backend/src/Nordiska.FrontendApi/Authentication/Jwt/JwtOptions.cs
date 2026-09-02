namespace Nordiska.FrontendApi.Authentication.Jwt;

public class JwtOptions
{
    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int TokenLifetimeInMinutes { get; set; }
}
