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

// Real Postgres, no fakes: opens and closes accounts through the API and checks the rows in BankingDbContext.
[Collection(PostgresCollection.Name)]
public class AccountLifecycleIntegrationTests : IAsyncLifetime
{
    private readonly PostgresAuthWebApplicationFactory _factory;
    private readonly List<long> _createdCustomerIds = new();

    public AccountLifecycleIntegrationTests(PostgresAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [PostgresFact]
    public async Task Create_PersistsAccountWithGeneratedAccountNumber()
    {
        var (client, customer) = await CreateLoggedInCustomerAsync();

        var response = await client.PostAsJsonAsync("/api/accounts", new OpenSavingsAccountRequest(
            CustomerId: customer.Id,
            AccountType: "Standard",
            AccountName: "Resekassa"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<SavingsAccountResponse>();
        created!.AccountNumber.Should().StartWith("NOR-");

        var stored = await GetStoredAccountAsync(created.Id);
        stored.Should().NotBeNull();
        stored!.CustomerId.Should().Be(customer.Id);
        stored.AccountNumber.Should().Be(created.AccountNumber);
        stored.AccountName.Should().Be("Resekassa");
        stored.AccountType.Should().Be("Standard");
        stored.Status.Should().Be("active");

        var accounts = await client.GetFromJsonAsync<List<SavingsAccountResponse>>("/api/accounts");
        accounts!.Select(a => a.Id).Should().Contain(created.Id);
    }

    [PostgresFact]
    public async Task Create_ForOtherCustomer_Returns404AndPersistsNothing()
    {
        var (client, _) = await CreateLoggedInCustomerAsync();
        var other = await PostgresTestData.CreateCustomerAsync(_factory.Services, "account");
        _createdCustomerIds.Add(other.Id);

        var response = await client.PostAsJsonAsync("/api/accounts", new OpenSavingsAccountRequest(
            CustomerId: other.Id,
            AccountType: "Standard"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
        (await db.SavingsAccounts.AnyAsync(a => a.CustomerId == other.Id)).Should().BeFalse();
    }

    [PostgresFact]
    public async Task Close_WithBalance_Returns409AndAccountStaysActive()
    {
        var (client, customer) = await CreateLoggedInCustomerAsync();
        var accountId = await OpenAccountAsync(client, customer.Id);

        var deposit = await client.PostAsJsonAsync("/api/transactions", new TransactionRequest(accountId, "deposit", 250m));
        deposit.StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await client.PostAsync($"/api/accounts/{accountId}/close", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await GetStoredAccountAsync(accountId))!.Status.Should().Be("active");
    }

    [PostgresFact]
    public async Task Close_EmptyAccount_SetsStatusClosedInDatabase()
    {
        var (client, customer) = await CreateLoggedInCustomerAsync();
        var accountId = await OpenAccountAsync(client, customer.Id);

        var response = await client.PostAsync($"/api/accounts/{accountId}/close", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var closed = await response.Content.ReadFromJsonAsync<SavingsAccountResponse>();
        closed!.Status.Should().Be("closed");

        var stored = await GetStoredAccountAsync(accountId);
        stored!.Status.Should().Be("closed");
        stored.UpdatedAt.Should().NotBeNull();
    }

    [PostgresFact]
    public async Task Close_AfterWithdrawingEverything_Succeeds()
    {
        var (client, customer) = await CreateLoggedInCustomerAsync();
        var accountId = await OpenAccountAsync(client, customer.Id);

        (await client.PostAsJsonAsync("/api/transactions", new TransactionRequest(accountId, "deposit", 400m)))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await client.PostAsJsonAsync("/api/transactions", new TransactionRequest(accountId, "withdrawal", 400m)))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await client.PostAsync($"/api/accounts/{accountId}/close", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetStoredAccountAsync(accountId))!.Status.Should().Be("closed");
    }

    public Task InitializeAsync() => Task.CompletedTask;

    // Remove the customers (and their accounts and ledger entries) so the shared CI database stays clean
    public Task DisposeAsync() => PostgresTestData.DeleteCustomersAsync(_factory.Services, _createdCustomerIds);

    private async Task<(HttpClient Client, Customer Customer)> CreateLoggedInCustomerAsync()
    {
        var customer = await PostgresTestData.CreateCustomerAsync(_factory.Services, "account");
        _createdCustomerIds.Add(customer.Id);

        var client = await PostgresTestData.CreateLoggedInClientAsync(_factory, customer);
        return (client, customer);
    }

    private static async Task<long> OpenAccountAsync(HttpClient client, long customerId)
    {
        var response = await client.PostAsJsonAsync("/api/accounts", new OpenSavingsAccountRequest(
            CustomerId: customerId,
            AccountType: "Standard"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<SavingsAccountResponse>();
        return created!.Id;
    }

    private async Task<SavingsAccount?> GetStoredAccountAsync(long accountId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
        return await db.SavingsAccounts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == accountId);
    }
}
