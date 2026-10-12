using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nordiska.FrontendApi.IntegrationTests.Postgres;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Contracts.Responses;
using Nordiska.Modules.Banking.Domain;
using Nordiska.Modules.Banking.Infrastructure.Db;

namespace Nordiska.FrontendApi.IntegrationTests.Banking;

[Collection(PostgresCollection.Name)]
public class FavoriteAccountsIntegrationTests : IAsyncLifetime
{
    private readonly PostgresAuthWebApplicationFactory _factory;
    private readonly List<long> _createdCustomerIds = new();

    public FavoriteAccountsIntegrationTests(PostgresAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [PostgresFact]
    public async Task SetFavorite_WhenUnderFour_Returns200AndPersistsIsFavoriteTrue()
    {
        var (client, customer) = await CreateLoggedInCustomerAsync();

        var openRes = await client.PostAsJsonAsync("/api/accounts", new OpenSavingsAccountRequest(
            CustomerId: customer.Id,
            AccountType: "standard",
            AccountName: "Mitt Sparkonto"));
        openRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await openRes.Content.ReadFromJsonAsync<SavingsAccountResponse>();

        var favRes = await client.PatchAsJsonAsync($"/api/accounts/{created!.Id}/favorite", new SetAccountFavoriteRequest(true));
        favRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await favRes.Content.ReadFromJsonAsync<SavingsAccountResponse>();
        updated!.IsFavorite.Should().BeTrue();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
        var stored = await db.SavingsAccounts.FindAsync(created.Id);
        stored.Should().NotBeNull();
        stored!.IsFavorite.Should().BeTrue();
    }

    [PostgresFact]
    public async Task SetFavorite_WhenAttemptingFifthFavorite_Returns400BadRequest()
    {
        var (client, customer) = await CreateLoggedInCustomerAsync();

        var accountIds = new List<long>();
        for (int i = 0; i < 5; i++)
        {
            var openRes = await client.PostAsJsonAsync("/api/accounts", new OpenSavingsAccountRequest(
                CustomerId: customer.Id,
                AccountType: "standard",
                AccountName: $"Konto {i + 1}"));
            openRes.StatusCode.Should().Be(HttpStatusCode.Created);
            var acc = await openRes.Content.ReadFromJsonAsync<SavingsAccountResponse>();
            accountIds.Add(acc!.Id);
        }

        // Mark first 4 accounts as favorites (should succeed)
        for (int i = 0; i < 4; i++)
        {
            var res = await client.PatchAsJsonAsync($"/api/accounts/{accountIds[i]}/favorite", new SetAccountFavoriteRequest(true));
            res.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Try marking 5th account as favorite (should fail with 400 Bad Request)
        var fifthRes = await client.PatchAsJsonAsync($"/api/accounts/{accountIds[4]}/favorite", new SetAccountFavoriteRequest(true));
        fifthRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [PostgresFact]
    public async Task GetAll_WithIsFavoriteFilter_ReturnsOnlyFavorites()
    {
        var (client, customer) = await CreateLoggedInCustomerAsync();

        var acc1 = await (await client.PostAsJsonAsync("/api/accounts", new OpenSavingsAccountRequest(customer.Id, AccountType: "standard", AccountName: "Fav 1"))).Content.ReadFromJsonAsync<SavingsAccountResponse>();
        var acc2 = await (await client.PostAsJsonAsync("/api/accounts", new OpenSavingsAccountRequest(customer.Id, AccountType: "standard", AccountName: "Non-fav"))).Content.ReadFromJsonAsync<SavingsAccountResponse>();
        var acc3 = await (await client.PostAsJsonAsync("/api/accounts", new OpenSavingsAccountRequest(customer.Id, AccountType: "standard", AccountName: "Fav 2"))).Content.ReadFromJsonAsync<SavingsAccountResponse>();

        await client.PatchAsJsonAsync($"/api/accounts/{acc1!.Id}/favorite", new SetAccountFavoriteRequest(true));
        await client.PatchAsJsonAsync($"/api/accounts/{acc3!.Id}/favorite", new SetAccountFavoriteRequest(true));

        var favoritesOnly = await client.GetFromJsonAsync<List<SavingsAccountResponse>>("/api/accounts?isFavorite=true");
        favoritesOnly.Should().NotBeNull();
        favoritesOnly!.Select(a => a.Id).Should().Contain(new[] { acc1.Id, acc3.Id });
        favoritesOnly.Select(a => a.Id).Should().NotContain(acc2!.Id);
    }

    [PostgresFact]
    public async Task SetFavorite_ForOtherCustomerAccount_Returns404NotFound()
    {
        var (client, _) = await CreateLoggedInCustomerAsync();
        var otherCustomer = await PostgresTestData.CreateCustomerAsync(_factory.Services, "favother");
        _createdCustomerIds.Add(otherCustomer.Id);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
            var otherAccount = new SavingsAccount
            {
                CustomerId = otherCustomer.Id,
                AccountNumber = "NOR-999991",
                AccountType = "standard",
                Balance = 100m,
                InterestRate = 0.025m,
                CreatedAt = DateTime.UtcNow
            };
            db.SavingsAccounts.Add(otherAccount);
            await db.SaveChangesAsync();

            var res = await client.PatchAsJsonAsync($"/api/accounts/{otherAccount.Id}/favorite", new SetAccountFavoriteRequest(true));
            res.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }

    private async Task<(HttpClient Client, Customer Customer)> CreateLoggedInCustomerAsync()
    {
        var customer = await PostgresTestData.CreateCustomerAsync(_factory.Services, "favtest");
        _createdCustomerIds.Add(customer.Id);
        var client = await PostgresTestData.CreateLoggedInClientAsync(_factory, customer);
        return (client, customer);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => PostgresTestData.DeleteCustomersAsync(_factory.Services, _createdCustomerIds);
}
