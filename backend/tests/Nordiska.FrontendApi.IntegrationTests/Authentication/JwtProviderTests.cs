using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
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
        _options = new JwtOptions
        {
            SecretKey = "TestSecretKeyThatIsVeryLongAndSecure123!",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            TokenLifetimeInMinutes = 15
        };

        _optionsMock = new Mock<IOptions<JwtOptions>>();
        _optionsMock
            .Setup(o => o.Value)
            .Returns(_options);

        var userStore = new Mock<IUserStore<Customer>>();

        _userManagerMock = new Mock<UserManager<Customer>>(
            userStore.Object,
            Mock.Of<IOptions<IdentityOptions>>(),
            Mock.Of<IPasswordHasher<Customer>>(),
            Array.Empty<IUserValidator<Customer>>(),
            Array.Empty<IPasswordValidator<Customer>>(),
            Mock.Of<ILookupNormalizer>(),
            new IdentityErrorDescriber(),
            Mock.Of<IServiceProvider>(),
            Mock.Of<ILogger<UserManager<Customer>>>());
    }

    [Fact]
    public async Task Generate_ShouldReturnValidJwtToken()
    {
        // Arrange
        var customer = new Customer
        {
            Id = 42,
            Email = "test@example.com",
            UserName = "test@example.com",
            Name = "Test Customer"
        };

        _userManagerMock
            .Setup(manager => manager.GetRolesAsync(customer))
            .ReturnsAsync(new List<string>
            {
                "Customer"
            });

        var provider = new JwtProvider(
            _optionsMock.Object,
            _userManagerMock.Object);

        // Act
        var token = await provider.Generate(customer);

        // Assert
        token.Should().NotBeNullOrEmpty();

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        jwtToken.Issuer.Should().Be(_options.Issuer);
        jwtToken.Audiences.Should().Contain(_options.Audience);

        var subClaim = jwtToken.Payload.Sub;
        subClaim.Should().Be(customer.Id.ToString());

        var emailClaim =
            jwtToken.Payload[JwtRegisteredClaimNames.Email]
                ?.ToString();

        emailClaim.Should().Be(customer.Email);

        var roleClaim = jwtToken.Claims
            .FirstOrDefault(c =>
                c.Type == "role" ||
                c.Type == ClaimTypes.Role)
            ?.Value;

        roleClaim.Should().Be("Customer");
    }
}