using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Nordiska.FrontendApi.Contracts.Requests;
using Nordiska.FrontendApi.IntegrationTests.Authentication;
using Nordiska.Modules.Banking.Contracts.Responses;
using Xunit;

namespace Nordiska.FrontendApi.IntegrationTests.Authorization;

// Anna (customer 1) owns accounts 1 and 2, Erik (customer 2) owns account 3.
// Another customer's data should look like it does not exist, so every attempt returns 404.
public class CustomerDataIsolationIntegrationTests : IClassFixture<CustomAuthWebApplicationFactory>
{
    private const string AnnaEmail = "anna@exempel.se";
    private const string ErikEmail = "erik@exempel.se";

    private readonly CustomAuthWebApplicationFactory _factory;

    public CustomerDataIsolationIntegrationTests(CustomAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string email = AnnaEmail)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "password123"));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        return client;
    }

    [Theory]
    [InlineData("GET", "/api/accounts/3")]
    [InlineData("POST", "/api/accounts/3/close")]
    [InlineData("GET", "/api/customers/2")]
    [InlineData("DELETE", "/api/customers/2")]
    [InlineData("GET", "/api/transactions?accountId=3")]
    [InlineData("GET", "/api/transactions/balance/3")]
    [InlineData("GET", "/api/reports/tax-report?accountId=3&year=2025")]
    public async Task OtherCustomersData_Returns_404NotFound(string method, string url)
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateAccount_ForOtherCustomer_Returns_404NotFound()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/accounts", new
        {
            customerId = 2,
            accountName = "Inte mitt konto",
            accountType = "saving",
            initialDeposit = 0m,
            interestRate = 0.01m
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateCustomer_ForOtherCustomer_Returns_404NotFound()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PatchAsJsonAsync("/api/customers/2", new { name = "Kapad" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Transfer_FromOtherCustomersAccount_Returns_404NotFound()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/transactions/transfer", new
        {
            sourceAccountId = 3,
            targetAccountId = 1,
            amount = 100m
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task InitiateTaxReport_ForOtherCustomersAccount_Returns_404NotFound()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/reports/tax-report", new { accountId = 3, year = 2025 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTransaction_BelongingToOtherCustomer_Returns_404NotFound()
    {
        // Transaction 1 is on Anna's account, so Erik must not see it
        var client = await CreateAuthenticatedClientAsync(ErikEmail);

        var response = await client.GetAsync("/api/transactions/1");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("/api/accounts/1")]
    [InlineData("/api/customers/1")]
    [InlineData("/api/transactions/1")]
    [InlineData("/api/transactions/balance/1")]
    [InlineData("/api/transactions?accountId=1")]
    public async Task OwnData_Returns_200Ok(string url)
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAccounts_ReturnsOnlyOwnAccounts()
    {
        var client = await CreateAuthenticatedClientAsync(ErikEmail);

        var accounts = await client.GetFromJsonAsync<List<SavingsAccountResponse>>("/api/accounts");

        accounts.Should().NotBeNullOrEmpty();
        accounts!.Should().OnlyContain(a => a.CustomerId == 2);
    }
}
