using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nordiska.FrontendApi.IntegrationTests.Postgres;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Contracts.Responses;
using Nordiska.Modules.Banking.Domain;
using Nordiska.Modules.Banking.Infrastructure.Db;

namespace Nordiska.FrontendApi.IntegrationTests.Banking;

// Real Postgres, no fakes except the Riksbank client: goes through the whole chain (auth cookie, controller,
// LoanService, LoanRepository) and checks that the loan, the ledger and the account balance end up consistent.
// Every test uses its own customer and accounts so the balances are never affected by other tests.
[Collection(PostgresCollection.Name)]
public class LoanFlowIntegrationTests : IAsyncLifetime
{
    private sealed class FakeRiksbankClient : IRiksbankClient
    {
        public Task<PolicyRateObservation> GetLatestPolicyRateAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new PolicyRateObservation(new DateOnly(2026, 9, 18), 1.75m));
    }

    private readonly PostgresAuthWebApplicationFactory _factory;
    private readonly List<long> _createdCustomerIds = new();

    public LoanFlowIntegrationTests(PostgresAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [PostgresFact]
    public async Task Apply_CreatesLoanAndPaysOutToAccount()
    {
        // Applying fetches the policy rate, so the Riksbank client is swapped to keep the test off the network
        var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IRiksbankClient>();
            services.AddSingleton<IRiksbankClient, FakeRiksbankClient>();
        }));
        var (client, customer) = await CreateLoggedInCustomerAsync(factory);
        var accountId = await SeedAccountAsync(customer.Id, balance: 1000m);

        var response = await client.PostAsJsonAsync("/api/loans", new ApplyForLoanRequest(100000m, 36, accountId));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<LoanResponse>();
        created!.Status.Should().Be("active");
        created.InterestRate.Should().Be(0.0675m);

        var stored = await GetStoredLoanAsync(created.Id);
        stored.CustomerId.Should().Be(customer.Id);
        stored.OutstandingAmount.Should().Be(100000m);

        var entries = await GetLedgerEntriesAsync(accountId);
        entries.Should().ContainSingle(e => e.Type == "deposit" && e.Amount == 100000m && e.Label == $"Utbetalning lån {created.LoanNumber}");
        (await GetStoredAccountAsync(accountId)).Balance.Should().Be(101000m);
    }

    [PostgresFact]
    public async Task Apply_OverTotalPersonalDebt_Returns409AndWritesNothing()
    {
        var (client, customer) = await CreateLoggedInCustomerAsync(_factory);
        var accountId = await SeedAccountAsync(customer.Id);
        await SeedLoanAsync(customer.Id, 450000m);

        var response = await client.PostAsJsonAsync("/api/loans", new ApplyForLoanRequest(60000m, 36, accountId));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await GetLedgerEntriesAsync(accountId)).Should().BeEmpty();
        (await GetStoredLoansAsync(customer.Id)).Should().ContainSingle();
    }

    [PostgresFact]
    public async Task Repay_LowersLoanAndWithdrawsFromAccount()
    {
        var (client, customer) = await CreateLoggedInCustomerAsync(_factory);
        var accountId = await SeedAccountAsync(customer.Id, balance: 20000m);
        var loanId = await SeedLoanAsync(customer.Id, 50000m);

        var response = await client.PostAsJsonAsync($"/api/loans/{loanId}/repayments", new RepayLoanRequest(15000m, accountId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<LoanResponse>();
        updated!.OutstandingAmount.Should().Be(35000m);

        (await GetStoredLoanAsync(loanId)).OutstandingAmount.Should().Be(35000m);
        (await GetLedgerEntriesAsync(accountId)).Should().ContainSingle(e => e.Type == "withdrawal" && e.Amount == -15000m);
        (await GetStoredAccountAsync(accountId)).Balance.Should().Be(5000m);
    }

    [PostgresFact]
    public async Task Repay_WholeLoan_MarksLoanRepaid()
    {
        var (client, customer) = await CreateLoggedInCustomerAsync(_factory);
        var accountId = await SeedAccountAsync(customer.Id, balance: 20000m);
        var loanId = await SeedLoanAsync(customer.Id, 20000m);

        var response = await client.PostAsJsonAsync($"/api/loans/{loanId}/repayments", new RepayLoanRequest(20000m, accountId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await GetStoredLoanAsync(loanId);
        stored.OutstandingAmount.Should().Be(0m);
        stored.Status.Should().Be(LoanStatus.Repaid);
        (await GetStoredAccountAsync(accountId)).Balance.Should().Be(0m);
    }

    [PostgresFact]
    public async Task Repay_InsufficientFunds_Returns409AndWritesNothing()
    {
        var (client, customer) = await CreateLoggedInCustomerAsync(_factory);
        var accountId = await SeedAccountAsync(customer.Id, balance: 100m);
        var loanId = await SeedLoanAsync(customer.Id, 50000m);

        var response = await client.PostAsJsonAsync($"/api/loans/{loanId}/repayments", new RepayLoanRequest(100.01m, accountId));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await GetStoredLoanAsync(loanId)).OutstandingAmount.Should().Be(50000m);
        (await GetLedgerEntriesAsync(accountId)).Should().ContainSingle();
        (await GetStoredAccountAsync(accountId)).Balance.Should().Be(100m);
    }

    [PostgresFact]
    public async Task Repay_MoreThanOwed_Returns409AndWritesNothing()
    {
        var (client, customer) = await CreateLoggedInCustomerAsync(_factory);
        var accountId = await SeedAccountAsync(customer.Id, balance: 20000m);
        var loanId = await SeedLoanAsync(customer.Id, 10000m);

        var response = await client.PostAsJsonAsync($"/api/loans/{loanId}/repayments", new RepayLoanRequest(10000.01m, accountId));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await GetStoredLoanAsync(loanId)).OutstandingAmount.Should().Be(10000m);
        (await GetStoredAccountAsync(accountId)).Balance.Should().Be(20000m);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => PostgresTestData.DeleteCustomersAsync(_factory.Services, _createdCustomerIds);

    private async Task<(HttpClient Client, Customer Customer)> CreateLoggedInCustomerAsync(WebApplicationFactory<Program> factory)
    {
        var customer = await PostgresTestData.CreateCustomerAsync(_factory.Services, "loan");
        _createdCustomerIds.Add(customer.Id);

        var client = await PostgresTestData.CreateLoggedInClientAsync(factory, customer);
        return (client, customer);
    }

    // The balance is the sum of the ledger, so a starting balance is seeded as a deposit entry
    private async Task<long> SeedAccountAsync(long customerId, decimal balance = 0m)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BankingDbContext>();

        var accountType = await db.AccountTypeConfigs
            .AsNoTracking()
            .FirstAsync();

        var account = new SavingsAccount
        {
            CustomerId = customerId,
            AccountNumber = $"NOR-T{Guid.NewGuid().ToString("N")[..12]}",
            AccountType = accountType.AccountType,
            Balance = balance,
            InterestRate = accountType.InterestRate,
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

    // Opened today so no interest has accrued and the amounts in the tests stay exact
    private async Task<long> SeedLoanAsync(long customerId, decimal principal)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BankingDbContext>();

        var loan = new Loan(customerId, Guid.NewGuid().ToString("N"), LoanType.Personal, principal, 0.0675m, DateOnly.FromDateTime(DateTime.UtcNow));
        db.Loans.Add(loan);
        await db.SaveChangesAsync();

        return loan.Id;
    }

    private async Task<Loan> GetStoredLoanAsync(long loanId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
        return await db.Loans.AsNoTracking().SingleAsync(l => l.Id == loanId);
    }

    private async Task<List<Loan>> GetStoredLoansAsync(long customerId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
        return await db.Loans.AsNoTracking().Where(l => l.CustomerId == customerId).ToListAsync();
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
}
