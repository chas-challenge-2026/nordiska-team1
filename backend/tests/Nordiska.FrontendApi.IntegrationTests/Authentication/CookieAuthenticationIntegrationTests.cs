using System;
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
    public async Task BankIdCollect_WithValidCustomer_Sets_HttpOnly_AuthCookie()
    {
        // Arrange
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
            AllowAutoRedirect = false
        });

        // 1. Initiate BankID with seeded user's personal number
        var initiateResponse = await client.PostAsJsonAsync("/api/auth/bankid/initiate", new
        {
            personalNum = "198202116050"
        });
        initiateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var initiateContent = await initiateResponse.Content.ReadAsStringAsync();
        using var initDoc = JsonDocument.Parse(initiateContent);
        var orderRef = initDoc.RootElement.GetProperty("orderRef").GetString();

        // 2. Collect BankID (polling until COMPLETE in simulated flow)
        HttpResponseMessage collectResponse = null!;
        for (var i = 0; i < 15; i++)
        {
            collectResponse = await client.PostAsJsonAsync("/api/auth/bankid/collect", new
            {
                orderRef = orderRef
            });

            var content = await collectResponse.Content.ReadAsStringAsync();
            if (!collectResponse.IsSuccessStatusCode)
            {
                break;
            }

            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("status", out var s) || doc.RootElement.TryGetProperty("Status", out s))
            {
                if (string.Equals(s.GetString(), "COMPLETE", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
            }
            await Task.Delay(200);
        }

        // Assert
        var failureBody = await collectResponse.Content.ReadAsStringAsync();
        collectResponse.StatusCode.Should().Be(HttpStatusCode.OK, because: failureBody);
        collectResponse.Headers.Contains("Set-Cookie").Should().BeTrue();

        var setCookieHeader = collectResponse.Headers.GetValues("Set-Cookie").FirstOrDefault();
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
            HandleCookies = true,
            AllowAutoRedirect = false
        });

        // Act 1: Initiate & Collect BankID with Erik's personal number
        var initiateResponse = await client.PostAsJsonAsync("/api/auth/bankid/initiate", new
        {
            personalNum = "197903142380"
        });
        initiateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var initiateContent = await initiateResponse.Content.ReadAsStringAsync();
        using var initDoc = JsonDocument.Parse(initiateContent);
        var orderRef = initDoc.RootElement.GetProperty("orderRef").GetString();

        HttpResponseMessage collectResponse = null!;
        for (var i = 0; i < 15; i++)
        {
            collectResponse = await client.PostAsJsonAsync("/api/auth/bankid/collect", new
            {
                orderRef = orderRef
            });

            var content = await collectResponse.Content.ReadAsStringAsync();
            if (!collectResponse.IsSuccessStatusCode)
            {
                break;
            }

            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("status", out var s) || doc.RootElement.TryGetProperty("Status", out s))
            {
                if (string.Equals(s.GetString(), "COMPLETE", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
            }
            await Task.Delay(200);
        }
        var failureBody = await collectResponse.Content.ReadAsStringAsync();
        collectResponse.StatusCode.Should().Be(HttpStatusCode.OK, because: failureBody);

        // Act 2: Access protected /api/auth/me endpoint using the automatically attached cookie
        var meResponse = await client.GetAsync("/api/auth/me");

        // Assert
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var meContent = await meResponse.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(meContent);
        var email = jsonDoc.RootElement.GetProperty("email").GetString();
        email.Should().Be("erik@exempel.se");
    }

    [Fact]
    public async Task Register_WithValidCustomer_Sets_HttpOnly_AuthCookie()
    {
        // Arrange
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });

        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var registerPayload = new
        {
            name = $"Test Person {uniqueId}",
            personalNum = $"19900101{Random.Shared.Next(1000, 9999)}",
            email = $"test_{uniqueId}@example.com",
            phoneNumber = "+46701234567"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/register", registerPayload);

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

    [Fact]
    public async Task Register_Then_BankIdLogin_LogsIn_Registered_Customer()
    {
        // Arrange
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });

        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var customPersonalNum = $"19910203{Random.Shared.Next(1000, 9999)}";
        var customEmail = $"customer_{uniqueId}@example.com";

        var registerPayload = new
        {
            name = $"New Customer {uniqueId}",
            personalNum = customPersonalNum,
            email = customEmail,
            phoneNumber = "+46709998877"
        };

        // Act 1: Register customer
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", registerPayload);
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act 2: Clear cookie via logout to simulate logging in fresh
        await client.PostAsync("/api/auth/logout", null);

        // Act 3: Initiate and Collect BankID with the newly registered customer's personal number
        var initiateResponse = await client.PostAsJsonAsync("/api/auth/bankid/initiate", new
        {
            personalNum = customPersonalNum
        });
        initiateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var initiateContent = await initiateResponse.Content.ReadAsStringAsync();
        using var initDoc = JsonDocument.Parse(initiateContent);
        var orderRef = initDoc.RootElement.GetProperty("orderRef").GetString();

        HttpResponseMessage collectResponse = null!;
        for (var i = 0; i < 15; i++)
        {
            collectResponse = await client.PostAsJsonAsync("/api/auth/bankid/collect", new
            {
                orderRef = orderRef
            });

            var content = await collectResponse.Content.ReadAsStringAsync();
            if (!collectResponse.IsSuccessStatusCode)
            {
                break;
            }

            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("status", out var s) || doc.RootElement.TryGetProperty("Status", out s))
            {
                if (string.Equals(s.GetString(), "COMPLETE", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
            }
            await Task.Delay(200);
        }
        collectResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act 4: Query /api/auth/me to confirm it is the newly registered customer
        var meResponse = await client.GetAsync("/api/auth/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var meContent = await meResponse.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(meContent);
        var email = jsonDoc.RootElement.GetProperty("email").GetString();
        email.Should().Be(customEmail);
    }

    [Fact]
    public async Task EmailPassword_Login_Sets_HttpOnly_AuthCookie_And_Returns_Customer()
    {
        // Arrange
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });

        // Act 1: Login with seeded customer Anna
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "anna@exempel.se",
            password = "password123"
        });

        // Assert 1: Successful login and auth cookie set
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        loginResponse.Headers.Contains("Set-Cookie").Should().BeTrue();

        var content = await loginResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var name = doc.RootElement.GetProperty("name").GetString();
        name.Should().Be("Anna Smith");

        // Act 2: Access protected /api/auth/me endpoint
        var meResponse = await client.GetAsync("/api/auth/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var meContent = await meResponse.Content.ReadAsStringAsync();
        using var meDoc = JsonDocument.Parse(meContent);
        var email = meDoc.RootElement.GetProperty("email").GetString();
        email.Should().Be("anna@exempel.se");
    }
}
