using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nordiska.FrontendApi.Authentication;
using Nordiska.FrontendApi.Authentication.Jwt;
using Nordiska.FrontendApi.Contracts.Requests;
using Nordiska.FrontendApi.Contracts.Responses;
using Nordiska.FrontendApi.Extensions;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Domain;
using Xunit;

namespace Nordiska.FrontendApi.IntegrationTests.Authentication;

public class TestAuthService : IAuthService
{
    private readonly IJwtProvider _jwtProvider;
    private static readonly ConcurrentDictionary<string, Customer> _customers = new();
    private static readonly ConcurrentDictionary<string, string> _orders = new();

    static TestAuthService()
    {
        var anna = new Customer
        {
            Id = 1,
            Name = "Anna Smith",
            Email = "anna@exempel.se",
            PersonalNum = "198202116050",
            PhoneNumber = "+46701112233"
        };
        var erik = new Customer
        {
            Id = 2,
            Name = "Erik Svensson",
            Email = "erik@exempel.se",
            PersonalNum = "197903142380",
            PhoneNumber = "+46702223344"
        };

        _customers[anna.PersonalNum] = anna;
        _customers[anna.Email] = anna;
        _customers["anna@example.com"] = anna;
        _customers[erik.PersonalNum] = erik;
        _customers[erik.Email] = erik;
        _customers["erik@example.com"] = erik;
    }

    public TestAuthService(IJwtProvider jwtProvider)
    {
        _jwtProvider = jwtProvider;
    }

    public Task<AuthenticationResultDto> InitiateBankIdAsync(BankIdInitiateRequest request, string clientIp)
    {
        var cleanPersonalNum = request.PersonalNum?.Replace("-", "").Trim() ?? "198202116050";
        var orderRef = Guid.NewGuid().ToString();
        _orders[orderRef] = cleanPersonalNum;

        var dto = new BankIdInitiateResponseDto(orderRef, "test-auto-start-token", "test-qr-code", "test-qr-secret");
        return Task.FromResult(new AuthenticationResultDto(true, null, InitiateData: dto));
    }

    public async Task<AuthenticationResultDto> CollectBankIdAsync(BankIdCollectRequest request, HttpResponse response)
    {
        if (!_orders.TryGetValue(request.OrderRef, out var personalNum))
        {
            personalNum = "198202116050";
        }

        if (!_customers.TryGetValue(personalNum, out var customer))
        {
            customer = new Customer
            {
                Id = Random.Shared.Next(100, 9999),
                Name = $"BankID User {personalNum}",
                Email = $"user_{personalNum}@nordiska.se",
                PersonalNum = personalNum,
                PhoneNumber = "+46700000000"
            };
            _customers[personalNum] = customer;
            _customers[customer.Email] = customer;
        }

        var token = await _jwtProvider.Generate(customer);
        response.AppendAuthCookie(token, 15);

        var completeData = new BankIdCollectResponseDto(
            "COMPLETE",
            null,
            new CustomerResponseDto(customer.Id, customer.Email ?? string.Empty, customer.Name)
        );

        return new AuthenticationResultDto(true, null, Token: token, CollectData: completeData);
    }

    public async Task<AuthenticationResultDto> RegisterCustomerAsync(RegisterCustomerRequestDto request, HttpResponse response)
    {
        var cleanPersonalNum = request.PersonalNum.Replace("-", "").Trim();
        var customer = new Customer
        {
            Id = Random.Shared.Next(100, 9999),
            Name = request.Name,
            Email = request.Email,
            PersonalNum = cleanPersonalNum,
            PhoneNumber = request.PhoneNumber
        };

        _customers[cleanPersonalNum] = customer;
        _customers[request.Email] = customer;

        var token = await _jwtProvider.Generate(customer);
        response.AppendAuthCookie(token, 15);

        return new AuthenticationResultDto(true, null, Token: token);
    }

    public async Task<AuthenticationResultDto> LoginAsync(LoginRequest request, HttpResponse response)
    {
        if (_customers.TryGetValue(request.Email, out var customer))
        {
            var token = await _jwtProvider.Generate(customer);
            response.AppendAuthCookie(token, 15);
            var completeData = new BankIdCollectResponseDto(
                "COMPLETE",
                null,
                new CustomerResponseDto(customer.Id, customer.Email ?? string.Empty, customer.Name)
            );
            return new AuthenticationResultDto(true, null, Token: token, CollectData: completeData);
        }

        return new AuthenticationResultDto(false, "Ogiltig e-postadress eller lösenord.");
    }
}

public class CustomAuthWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAuthService>();
            services.AddScoped<IAuthService, TestAuthService>();
        });
    }
}

public class CookieAuthenticationIntegrationTests : IClassFixture<CustomAuthWebApplicationFactory>
{
    private readonly CustomAuthWebApplicationFactory _factory;

    public CookieAuthenticationIntegrationTests(CustomAuthWebApplicationFactory factory)
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
