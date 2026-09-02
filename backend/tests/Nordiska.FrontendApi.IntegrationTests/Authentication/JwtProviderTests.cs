using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Nordiska.FrontendApi.Authentication.Jwt;
using Xunit;

namespace Nordiska.FrontendApi.IntegrationTests.Authentication;

public class JwtProviderTests
{
    private readonly Mock<IOptions<JwtOptions>> _optionsMock;
    private readonly JwtOptions _options;

    public JwtProviderTests()
    {
        _optionsMock = new Mock<IOptions<JwtOptions>>();
        _options = new JwtOptions
        {
            SecretKey = "TestSecretKeyThatIsVeryLongAndSecure123!",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            TokenLifetimeInMinutes = 15
        };
        _optionsMock.Setup(o => o.Value).Returns(_options);
    }

    [Fact]
    public void Generate_ShouldReturnValidJwtToken()
    {
        // Arrange
        var provider = new JwtProvider(_optionsMock.Object);
        long customerId = 42;
        string email = "test@example.com";
        string role = "Customer";

        // Act
        var token = provider.Generate(customerId, email, role);

        // Assert
        token.Should().NotBeNullOrEmpty();

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        jwtToken.Issuer.Should().Be(_options.Issuer);
        jwtToken.Audiences.Should().Contain(_options.Audience);

        // Verify claims
        var subClaim = jwtToken.Payload.Sub;
        subClaim.Should().Be(customerId.ToString());

        var emailClaim = jwtToken.Payload[JwtRegisteredClaimNames.Email]?.ToString();
        emailClaim.Should().Be(email);

        var roleClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "role" || c.Type == ClaimTypes.Role)?.Value;
        roleClaim.Should().Be(role);
    }
}
