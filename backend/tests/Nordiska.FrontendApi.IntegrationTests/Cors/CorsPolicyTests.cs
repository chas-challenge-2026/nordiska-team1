using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Nordiska.FrontendApi.IntegrationTests.Cors;

public class CorsPolicyTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public CorsPolicyTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Preflight_FromAllowedViteOrigin_Returns_CorsHeaders()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/auth/login");
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");

        // Act
        using var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
        response.Headers.GetValues("Access-Control-Allow-Origin").Should().Contain("http://localhost:5173");
        response.Headers.GetValues("Access-Control-Allow-Credentials").Should().Contain("true");
    }

    [Fact]
    public async Task Preflight_FromUntrustedOrigin_DoesNotInclude_AllowOriginHeader()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/auth/login");
        request.Headers.Add("Origin", "http://malicious-site.com");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        // Act
        using var response = await _client.SendAsync(request);

        // Assert
        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact]
    public async Task Preflight_DeleteMethod_IsNotAllowed_ByPolicy()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Options, "/weatherforecast");
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", "DELETE");

        // Act
        using var response = await _client.SendAsync(request);

        // Assert: DELETE should not be in allowed methods header
        if (response.Headers.TryGetValues("Access-Control-Allow-Methods", out var allowedMethods))
        {
            string.Join(",", allowedMethods).Should().NotContain("DELETE");
        }
    }
}
