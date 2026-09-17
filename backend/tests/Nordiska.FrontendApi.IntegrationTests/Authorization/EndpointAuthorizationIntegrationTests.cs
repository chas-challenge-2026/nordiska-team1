using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nordiska.FrontendApi.IntegrationTests.Authentication;
using Xunit;

namespace Nordiska.FrontendApi.IntegrationTests.Authorization;

public class EndpointAuthorizationIntegrationTests : IClassFixture<CustomAuthWebApplicationFactory>
{
    // Every endpoint that is reachable without logging in. Adding a new public endpoint
    // should be a conscious decision, so it has to be added here as well.
    private static readonly string[] AnonymousEndpoints =
    {
        "GET api/interest-rates",
        "POST api/auth/bankid/initiate",
        "POST api/auth/bankid/collect",
        "POST api/auth/register",
        "POST api/auth/login",
        "POST api/auth/logout",
        "GET api/faqs/{id:int}",
        "GET api/faqs/search",

        // Registered by AddBankIdAuth, ActiveLogin marks its own login flow as anonymous
        "GET ActiveLogin/BankId/Auth",
        "POST ActiveLogin/BankId/Auth/Api/Initialize",
        "POST ActiveLogin/BankId/Auth/Api/Status",
        "POST ActiveLogin/BankId/Auth/Api/QrCode",
        "POST ActiveLogin/BankId/Auth/Api/Cancel",

        // Dev tooling, only mapped in Development
        "GET /health/database",
        "GET,HEAD /openapi/{documentName}.json",
        "GET /scalar/{documentName?}",
        "GET /scalar/scalar.js",
        "GET /scalar/scalar.aspnetcore.js",
        "GET /scalar/favicon.svg",

        // SPA fallback to index.html
        "GET,HEAD {*path:nonfile}"
    };

    private readonly CustomAuthWebApplicationFactory _factory;

    public EndpointAuthorizationIntegrationTests(CustomAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void FallbackPolicy_RequiresAuthenticatedUser()
    {
        var options = _factory.Services.GetRequiredService<IOptions<AuthorizationOptions>>().Value;

        options.FallbackPolicy.Should().NotBeNull();
        options.FallbackPolicy!.Requirements.Should().ContainSingle(r => r is DenyAnonymousAuthorizationRequirement);
    }

    [Fact]
    public void AnonymousEndpoints_MatchAllowlist()
    {
        var anonymous = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(Describe)
            .ToList();

        anonymous.Should().BeEquivalentTo(AnonymousEndpoints, "these are the anonymous endpoints right now: {0}", string.Join(" | ", anonymous));
    }

    [Theory]
    [InlineData("GET", "/api/auth/me")]
    [InlineData("GET", "/api/accounts")]
    [InlineData("GET", "/api/accounts/1")]
    [InlineData("POST", "/api/accounts/1/close")]
    [InlineData("GET", "/api/customers/1")]
    [InlineData("POST", "/api/customers")]
    [InlineData("DELETE", "/api/customers/1")]
    [InlineData("GET", "/api/transactions/1")]
    [InlineData("GET", "/api/transactions/balance/1")]
    [InlineData("POST", "/api/transactions/transfer")]
    [InlineData("GET", "/api/reports/jobs/job_123")]
    [InlineData("GET", "/api/reports/tax-report?accountId=1&year=2025")]
    [InlineData("GET", "/api/test/secure")]
    public async Task ProtectedEndpoint_WithoutAuthCookie_Returns_401Unauthorized(string method, string url)
    {
        var client = _factory.CreateClient();

        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method is "POST")
        {
            request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        }

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static string Describe(RouteEndpoint endpoint)
    {
        var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods;
        var verb = methods is { Count: > 0 } ? string.Join(",", methods) : "*";
        return $"{verb} {endpoint.RoutePattern.RawText}";
    }
}
