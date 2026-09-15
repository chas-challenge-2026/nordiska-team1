using Nordiska.Modules.Banking.Domain;

namespace Nordiska.FrontendApi.Authentication.Jwt;

public interface IJwtProvider
{
    public Task<string> Generate(Customer customer);
}
