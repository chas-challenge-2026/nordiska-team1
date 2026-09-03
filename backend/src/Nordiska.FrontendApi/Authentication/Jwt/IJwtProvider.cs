namespace Nordiska.FrontendApi.Authentication.Jwt;

public interface IJwtProvider
{
    string Generate(long customerId, string email, string role);
}
