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
}
