using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Moq;
using Nordiska.FrontendApi.Authentication.Jwt;
using Nordiska.Modules.Banking.Domain;
using Xunit;

namespace Nordiska.FrontendApi.IntegrationTests.Authentication;

public class JwtProviderTests
{
    private readonly Mock<IOptions<JwtOptions>> _optionsMock;
    private readonly Mock<UserManager<Customer>> _userManagerMock;
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

        _optionsMock
            .Setup(o => o.Value)
            .Returns(_options);

        var userStoreMock = new Mock<IUserStore<Customer>>();

        _userManagerMock = new Mock<UserManager<Customer>>(
            userStoreMock.Object,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);
    }


    [Fact]
    public async Task Generate_ShouldReturnValidJwtTokenAsync()
    {
        // Arrange
        var customer = new Customer{Id= 42, Email= "test@example.com"};
        var roles = new List<string>{"Customer"};

        _userManagerMock
            .Setup(m => m.GetRolesAsync(customer))
            .ReturnsAsync(roles);

        var provider = new JwtProvider(_optionsMock.Object, _userManagerMock.Object);


        // Act
        var token = await provider.Generate(customer);


        // Assert
        token.Should().NotBeNullOrEmpty();

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        jwtToken.Issuer.Should().Be(_options.Issuer);
        jwtToken.Audiences.Should().Contain(_options.Audience);

        // Verify claims
        jwtToken.Payload.Sub.Should().Be(customer.Id.ToString());

        var emailClaim = jwtToken.Payload[
            JwtRegisteredClaimNames.Email]?.ToString();

        emailClaim.Should().Be(customer.Email);

        var roleClaim = jwtToken.Claims
            .FirstOrDefault(c =>
                c.Type == "role" ||
                c.Type == ClaimTypes.Role)
            ?.Value;

        roleClaim.Should().Be("Customer");
    }
}
