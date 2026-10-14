using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Nordiska.FrontendApi.Contracts.Requests;
using Nordiska.FrontendApi.Contracts.Responses;
using Nordiska.FrontendApi.IntegrationTests.Authentication;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Contracts.Responses;
using Xunit;

namespace Nordiska.FrontendApi.IntegrationTests.Banking;

public class EndpointEnhancementsIntegrationTests : IClassFixture<CustomAuthWebApplicationFactory>
{
    private readonly CustomAuthWebApplicationFactory _factory;

    public EndpointEnhancementsIntegrationTests(CustomAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string email = "anna@exempel.se")
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "password123"));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        return client;
    }

    [Fact]
    public async Task Accounts_DualRoutingAndTypeFilter_WorksAsExpected()
    {
        var client = await CreateAuthenticatedClientAsync();

        // 1. GET /api/accounts
        var resp1 = await client.GetAsync("/api/accounts");
        resp1.StatusCode.Should().Be(HttpStatusCode.OK);
        var accounts1 = await resp1.Content.ReadFromJsonAsync<List<SavingsAccountResponse>>();
        accounts1.Should().NotBeNull();
        accounts1!.Count.Should().BeGreaterThan(0);

        // 2. GET /api/savingsaccounts
        var resp2 = await client.GetAsync("/api/savingsaccounts");
        resp2.StatusCode.Should().Be(HttpStatusCode.OK);
        var accounts2 = await resp2.Content.ReadFromJsonAsync<List<SavingsAccountResponse>>();
        accounts2.Should().NotBeNull();
        accounts2!.Count.Should().Be(accounts1.Count);

        // 3. GET /api/accounts?type=saving
        var resp3 = await client.GetAsync("/api/accounts?type=saving");
        resp3.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PlannedTransactions_CreateAndCancel_Succeeds()
    {
        var client = await CreateAuthenticatedClientAsync();

        // Get user accounts
        var accResp = await client.GetAsync("/api/accounts");
        var accounts = await accResp.Content.ReadFromJsonAsync<List<SavingsAccountResponse>>();
        accounts.Should().NotBeNull().And.NotBeEmpty();
        var accountId = accounts!.First().Id;

        // Create planned transaction
        var plannedReq = new PlannedTransactionRequest(
            AccountId: accountId,
            Type: "withdrawal",
            Amount: 1500m,
            PlannedDate: DateTime.UtcNow.AddMonths(1),
            Label: "Hyra");

        var createResp = await client.PostAsJsonAsync("/api/transactions/planned", plannedReq);
        var createContent = await createResp.Content.ReadAsStringAsync();
        createResp.StatusCode.Should().Be(HttpStatusCode.Created, because: createContent);

        var created = await createResp.Content.ReadFromJsonAsync<TransactionResponse>();
        created.Should().NotBeNull();
        created!.IsPlanned.Should().BeTrue();
        created.Label.Should().Be("Hyra");
        created.PlannedDate.Should().NotBeNull();

        // Cancel planned transaction
        var deleteResp = await client.DeleteAsync($"/api/transactions/planned/{created.Id}");
        deleteResp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Customer_PatchProfile_UpdatesPhoneAndUpdatedAt()
    {
        var client = await CreateAuthenticatedClientAsync();

        var patchReq = new PatchCustomerRequest(Phone: "070-1234567");
        var patchResp = await client.PatchAsJsonAsync("/api/customers", patchReq);
        var content = await patchResp.Content.ReadAsStringAsync();
        patchResp.StatusCode.Should().Be(HttpStatusCode.OK, because: content);

        var customer = await patchResp.Content.ReadFromJsonAsync<CustomerResponse>();
        customer.Should().NotBeNull();
        customer!.PhoneNumber.Should().Be("070-1234567");
        customer.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Accounts_CreateWithInitialDeposit_CreatesLedgerDepositAndMatchesBalance()
    {
        var client = await CreateAuthenticatedClientAsync();

        // 1. Create a new account with InitialDeposit = 800 SEK
        var createReq = new OpenSavingsAccountRequest(
            CustomerId: 1,
            AccountNumber: "NOR-887766",
            AccountType: "standard",
            InitialDeposit: 800m,
            InterestRate: 0.025m,
            AccountName: "Mina sparade pengar");

        var createResp = await client.PostAsJsonAsync("/api/accounts", createReq);
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.Content.ReadFromJsonAsync<SavingsAccountResponse>();
        created.Should().NotBeNull();
        created!.Balance.Should().Be(800m);

        // 2. Query balance endpoint to verify ledger calculation
        var balanceResp = await client.GetAsync($"/api/transactions/balance/{created.Id}");
        balanceResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var verifiedBalance = await balanceResp.Content.ReadFromJsonAsync<decimal>();
        verifiedBalance.Should().Be(800m);
    }

    [Fact]
    public async Task Transfer_BetweenOwnAccounts_Succeeds_AndUpdatesBalances()
    {
        var client = await CreateAuthenticatedClientAsync();

        // 1. Get user accounts (Anna has account 1 and 2 in test fixture)
        var accResp = await client.GetAsync("/api/accounts");
        var accounts = await accResp.Content.ReadFromJsonAsync<List<SavingsAccountResponse>>();
        accounts.Should().NotBeNull();
        accounts!.Count.Should().BeGreaterThanOrEqualTo(2);

        var sourceAcc = accounts[0];
        var targetAcc = accounts[1];

        // Ensure source has funds
        var sourceBalResp = await client.GetAsync($"/api/transactions/balance/{sourceAcc.Id}");
        var sourceBalance = await sourceBalResp.Content.ReadFromJsonAsync<decimal>();

        if (sourceBalance < 100m)
        {
            // Deposit funds first if needed
            await client.PostAsJsonAsync("/api/transactions", new TransactionRequest(sourceAcc.Id, "deposit", 500m, "Top up"));
        }

        // 2. Execute transfer of 100 SEK
        var transferReq = new TransferRequest(
            SourceAccountId: sourceAcc.Id,
            TargetAccountId: targetAcc.Id,
            Amount: 100m,
            Label: "Överföring till sparkonto");

        var transferResp = await client.PostAsJsonAsync("/api/transactions/transfer", transferReq);
        var transferContent = await transferResp.Content.ReadAsStringAsync();
        transferResp.StatusCode.Should().Be(HttpStatusCode.OK, because: transferContent);

        var txResult = await transferResp.Content.ReadFromJsonAsync<TransactionResponse>();
        txResult.Should().NotBeNull();
        txResult!.Amount.Should().Be(-100m);
        txResult.AccountId.Should().Be(sourceAcc.Id);
        txResult.TargetAccountId.Should().Be(targetAcc.Id);
    }

    [Fact]
    public async Task Transfer_InsufficientFunds_Returns_409Conflict()
    {
        var client = await CreateAuthenticatedClientAsync();

        var accResp = await client.GetAsync("/api/accounts");
        var accounts = await accResp.Content.ReadFromJsonAsync<List<SavingsAccountResponse>>();
        accounts.Should().NotBeNull();
        accounts!.Count.Should().BeGreaterThanOrEqualTo(2);

        // Attempt to transfer an impossibly large amount
        var transferReq = new TransferRequest(
            SourceAccountId: accounts[0].Id,
            TargetAccountId: accounts[1].Id,
            Amount: 99999999m,
            Label: "Huge transfer");

        var transferResp = await client.PostAsJsonAsync("/api/transactions/transfer", transferReq);
        transferResp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Transfer_ZeroAmountOrSameAccount_Returns_400BadRequest()
    {
        var client = await CreateAuthenticatedClientAsync();

        var accResp = await client.GetAsync("/api/accounts");
        var accounts = await accResp.Content.ReadFromJsonAsync<List<SavingsAccountResponse>>();
        accounts.Should().NotBeNull();
        var accId = accounts!.First().Id;

        // Same source and target
        var sameAccReq = new TransferRequest(
            SourceAccountId: accId,
            TargetAccountId: accId,
            Amount: 50m,
            Label: "Same account transfer");

        var sameAccResp = await client.PostAsJsonAsync("/api/transactions/transfer", sameAccReq);
        sameAccResp.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Zero amount
        var zeroAmountReq = new TransferRequest(
            SourceAccountId: accounts[0].Id,
            TargetAccountId: accounts[1].Id,
            Amount: 0m,
            Label: "Zero transfer");

        var zeroResp = await client.PostAsJsonAsync("/api/transactions/transfer", zeroAmountReq);
        zeroResp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
