using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Nordiska.FrontendApi.IntegrationTests.Authentication;
using Xunit;

namespace Nordiska.FrontendApi.IntegrationTests.Diagnostics;

public class HealthCheckIntegrationTests : IClassFixture<CustomAuthWebApplicationFactory>
{
    private readonly CustomAuthWebApplicationFactory _factory;

    public HealthCheckIntegrationTests(CustomAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetHealth_ReturnsValidHealthReport()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert - verified across all environments (in CI where Postgres is running -> 200 OK Healthy, or 503 Unhealthy if down)
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);
        var content = await response.Content.ReadAsStringAsync();
        if (response.StatusCode == HttpStatusCode.OK)
        {
            content.Should().Be("Healthy");
        }
        else
        {
            content.Should().Be("Unhealthy");
        }
    }

    [Fact]
    public async Task GetHealthDatabase_ReturnsValidDatabaseStatus()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health/database");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);
    }
}

