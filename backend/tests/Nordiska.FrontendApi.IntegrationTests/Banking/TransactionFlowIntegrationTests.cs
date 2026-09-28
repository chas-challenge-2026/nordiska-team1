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

// Real Postgres, no fakes: goes through the whole chain (auth cookie, controller, TransactionService,
// repositories) and checks what actually ended up in the ledger. Every test uses its own customer
// and accounts so the balances are never affected by other tests.
[Collection(PostgresCollection.Name)]
public class TransactionFlowIntegrationTests : IAsyncLifetime
{
    private readonly PostgresAuthWebApplicationFactory _factory;
    private readonly List<long> _createdCustomerIds = new();

    public TransactionFlowIntegrationTests(PostgresAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [PostgresFact]
    public async Task Deposit_WritesLedgerEntryAndUpdatesBalance()
    {
        var (client, customer) = await CreateLoggedInCustomerAsync();
        var accountId = await SeedAccountAsync(customer.Id);

        var response = await client.PostAsJsonAsync("/api/transactions", new TransactionRequest(accountId, "deposit", 1500m, "Lön"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<TransactionResponse>();
        created!.Amount.Should().Be(1500m);

        var entries = await GetLedgerEntriesAsync(accountId);
        entries.Should().ContainSingle();
        entries[0].Id.Should().Be(created.Id);
        entries[0].Type.Should().Be("deposit");
        entries[0].Amount.Should().Be(1500m);
        entries[0].Label.Should().Be("Lön");

        (await GetBalanceAsync(client, accountId)).Should().Be(1500m);
        (await GetStoredAccountAsync(accountId)).Balance.Should().Be(1500m);
    }

    [PostgresFact]
    public async Task Withdrawal_WritesNegativeLedgerEntryAndLowersBalance()
    {
        var (client, customer) = await CreateLoggedInCustomerAsync();
        var accountId = await SeedAccountAsync(customer.Id, balance: 1000m);

        var response = await client.PostAsJsonAsync("/api/transactions", new TransactionRequest(accountId, "withdrawal", 400m));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var entries = await GetLedgerEntriesAsync(accountId);
        entries.Should().HaveCount(2);
        entries.Should().ContainSingle(e => e.Type == "withdrawal" && e.Amount == -400m);

        (await GetBalanceAsync(client, accountId)).Should().Be(600m);
        (await GetStoredAccountAsync(accountId)).Balance.Should().Be(600m);
    }

    [PostgresFact]
    public async Task Withdrawal_MoreThanBalance_Returns409AndWritesNothing()
    {
        var (client, customer) = await CreateLoggedInCustomerAsync();
        var accountId = await SeedAccountAsync(customer.Id, balance: 100m);

        var response = await client.PostAsJsonAsync("/api/transactions", new TransactionRequest(accountId, "withdrawal", 100.01m));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await GetLedgerEntriesAsync(accountId)).Should().ContainSingle();
        (await GetBalanceAsync(client, accountId)).Should().Be(100m);
    }

    [PostgresFact]
    public async Task Transfer_BetweenOwnAccounts_WritesBothEntriesAndMovesMoney()
    {
        var (client, customer) = await CreateLoggedInCustomerAsync();
        var sourceId = await SeedAccountAsync(customer.Id, balance: 2000m);
        var targetId = await SeedAccountAsync(customer.Id, balance: 500m);

        var response = await client.PostAsJsonAsync("/api/transactions/transfer", new TransferRequest(sourceId, targetId, 750m, "Buffert"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var sourceEntry = (await GetLedgerEntriesAsync(sourceId)).Single(e => e.Type == "transfer");
        sourceEntry.Amount.Should().Be(-750m);
        sourceEntry.TargetAccountId.Should().Be(targetId);

        var targetEntry = (await GetLedgerEntriesAsync(targetId)).Single(e => e.Type == "transfer");
        targetEntry.Amount.Should().Be(750m);
        targetEntry.TargetAccountId.Should().Be(sourceId);

        (await GetBalanceAsync(client, sourceId)).Should().Be(1250m);
        (await GetBalanceAsync(client, targetId)).Should().Be(1250m);
        (await GetStoredAccountAsync(sourceId)).Balance.Should().Be(1250m);
        (await GetStoredAccountAsync(targetId)).Balance.Should().Be(1250m);
    }

    [PostgresFact]
    public async Task Transfer_InsufficientFunds_Returns409AndWritesNothing()
    {
        var (client, customer) = await CreateLoggedInCustomerAsync();
        var sourceId = await SeedAccountAsync(customer.Id, balance: 300m);
        var targetId = await SeedAccountAsync(customer.Id);

        var response = await client.PostAsJsonAsync("/api/transactions/transfer", new TransferRequest(sourceId, targetId, 300.50m));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await GetLedgerEntriesAsync(sourceId)).Should().ContainSingle();
        (await GetLedgerEntriesAsync(targetId)).Should().BeEmpty();
        (await GetBalanceAsync(client, sourceId)).Should().Be(300m);
        (await GetBalanceAsync(client, targetId)).Should().Be(0m);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    // Remove the customers (and their accounts and ledger entries) so the shared CI database stays clean
    public Task DisposeAsync() => PostgresTestData.DeleteCustomersAsync(_factory.Services, _createdCustomerIds);

    private async Task<(HttpClient Client, Customer Customer)> CreateLoggedInCustomerAsync()
    {
        var customer = await PostgresTestData.CreateCustomerAsync(_factory.Services, "tx");
        _createdCustomerIds.Add(customer.Id);

        var client = await PostgresTestData.CreateLoggedInClientAsync(_factory, customer);
        return (client, customer);
    }

    // The balance is the sum of the ledger, so a starting balance is seeded as a deposit entry
    private async Task<long> SeedAccountAsync(long customerId, decimal balance = 0m)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BankingDbContext>();

        var account = new SavingsAccount
        {
            CustomerId = customerId,
            AccountNumber = $"NOR-T{Guid.NewGuid().ToString("N")[..12]}",
            AccountType = "Standard",
            Balance = balance,
            InterestRate = 0.025m,
            CreatedAt = DateTime.UtcNow
        };
        db.SavingsAccounts.Add(account);
        await db.SaveChangesAsync();

        if (balance > 0)
        {
            db.LedgerEntries.Add(new LedgerEntry
            {
                AccountId = account.Id,
                Type = "deposit",
                Amount = balance,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        return account.Id;
    }

    private async Task<List<LedgerEntry>> GetLedgerEntriesAsync(long accountId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
        return await db.LedgerEntries.AsNoTracking().Where(e => e.AccountId == accountId).ToListAsync();
    }

    private async Task<SavingsAccount> GetStoredAccountAsync(long accountId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
        return await db.SavingsAccounts.AsNoTracking().SingleAsync(a => a.Id == accountId);
    }

    private static async Task<decimal> GetBalanceAsync(HttpClient client, long accountId)
    {
        var response = await client.GetAsync($"/api/transactions/balance/{accountId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<decimal>();
    }
}
