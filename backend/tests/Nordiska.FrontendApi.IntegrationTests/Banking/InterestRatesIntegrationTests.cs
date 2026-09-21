using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nordiska.FrontendApi.IntegrationTests.Postgres;
using Nordiska.Modules.Banking.Contracts.Responses;
using Nordiska.Modules.Banking.Infrastructure.Db;

namespace Nordiska.FrontendApi.IntegrationTests.Banking;

// Real Postgres, no fakes: account_type_configs is seeded through a migration (SeedAccountTypeConfigs),
// so these tests verify the endpoint actually reads what migrations put in the database. Shares
// PostgresCollection with the other Postgres tests so they run against the same migrated database.
[Collection(PostgresCollection.Name)]
public class InterestRatesIntegrationTests
{
    private readonly PostgresAuthWebApplicationFactory _factory;

    public InterestRatesIntegrationTests(PostgresAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [PostgresFact]
    public async Task GetAll_ReturnsEveryAccountTypeConfigFromDatabase()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/interest-rates");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rates = await response.Content.ReadFromJsonAsync<List<AccountTypeConfigResponse>>();
        rates.Should().NotBeNull();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
        var stored = await db.AccountTypeConfigs.AsNoTracking().ToListAsync();

        rates!.Should().HaveCount(stored.Count);
        rates.Should().BeEquivalentTo(stored, options => options
            .ExcludingMissingMembers()
            .WithoutStrictOrdering());
    }

    [PostgresFact]
    public async Task GetAll_DoesNotRequireAuthentication()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/interest-rates");

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }
}
