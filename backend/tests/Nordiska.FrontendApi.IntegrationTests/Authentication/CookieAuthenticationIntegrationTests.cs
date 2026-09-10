using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Nordiska.FrontendApi.IntegrationTests.Authentication;

public class CookieAuthenticationIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CookieAuthenticationIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_WithValidCredentials_Sets_HttpOnly_AuthCookie()
    {
        // Arrange
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });

        var loginPayload = new
        {
            email = "anna@example.com",
            password = "password123"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", loginPayload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Contains("Set-Cookie").Should().BeTrue();

        var setCookieHeader = response.Headers.GetValues("Set-Cookie").FirstOrDefault();
        setCookieHeader.Should().NotBeNull();
        setCookieHeader.Should().Contain("access_token=");
        setCookieHeader.Should().Contain("httponly");
        setCookieHeader.Should().Contain("path=/");
    }

    [Fact]
    public async Task ProtectedEndpoint_WithAuthCookie_Returns_Ok_And_UserInfo()
    {
        // Arrange: Use cookie-handling HttpClient to simulate a browser session
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });

        var loginPayload = new
        {
            email = "anna@example.com",
            password = "password123"
        };

        // Act 1: Login
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", loginPayload);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act 2: Access protected /api/auth/me endpoint using the automatically attached cookie
        var meResponse = await client.GetAsync("/api/auth/me");

        // Assert
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await meResponse.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(content);
        var email = jsonDoc.RootElement.GetProperty("email").GetString();
        email.Should().Be("anna@example.com");
    }

    [Fact]
    public async Task Logout_Clears_AuthCookie()
    {
        // Arrange
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });

        // Act
        var response = await client.PostAsync("/api/auth/logout", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Contains("Set-Cookie").Should().BeTrue();

        var setCookieHeader = response.Headers.GetValues("Set-Cookie").FirstOrDefault();
        setCookieHeader.Should().NotBeNull();
        setCookieHeader.Should().Contain("access_token=");
    }
}
