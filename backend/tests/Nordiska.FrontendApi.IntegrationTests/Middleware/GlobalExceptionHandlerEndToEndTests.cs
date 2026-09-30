using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Nordiska.FrontendApi.Contracts.Requests;
using Nordiska.FrontendApi.IntegrationTests.Authentication;
using Xunit;

namespace Nordiska.FrontendApi.IntegrationTests.Middleware;

// Proves AddExceptionHandler<GlobalExceptionHandler>() + app.UseExceptionHandler() are actually
// wired together in Program.cs - the unit tests in GlobalExceptionHandlerTests only call the
// handler directly and can't catch a wiring regression.
public class GlobalExceptionHandlerEndToEndTests : IClassFixture<CustomAuthWebApplicationFactory>
{
    private readonly CustomAuthWebApplicationFactory _factory;

    public GlobalExceptionHandlerEndToEndTests(CustomAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetAccount_ThatDoesNotExist_Returns404ProblemDetailsWithTraceId()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("anna@exempel.se", "password123"));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await client.GetAsync("/api/accounts/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        body.GetProperty("status").GetInt32().Should().Be(404);
        body.GetProperty("title").GetString().Should().Be("Resurs hittades inte");
        body.GetProperty("detail").GetString().Should().Contain("999999");
        body.GetProperty("traceId").GetString().Should().NotBeNullOrEmpty();
    }
}
