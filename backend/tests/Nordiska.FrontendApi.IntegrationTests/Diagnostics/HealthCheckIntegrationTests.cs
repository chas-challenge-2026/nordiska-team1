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
    public async Task GetHealth_WhenDatabaseUnreachable_Returns503ServiceUnavailable_AndReportsUnhealthy()
    {
        // Arrange - in test environment with fake connection string
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert - exactly proves that HealthCheck actively validates database and catches failures!
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Be("Unhealthy");
    }

    [Fact]
    public async Task GetHealthDatabase_WhenDatabaseUnreachable_Returns503ServiceUnavailable()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health/database");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }
}
