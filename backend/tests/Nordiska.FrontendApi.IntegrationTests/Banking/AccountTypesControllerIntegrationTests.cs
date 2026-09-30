using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Nordiska.FrontendApi.Contracts.Requests;
using Nordiska.FrontendApi.IntegrationTests.Authentication;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Contracts.Responses;
using Xunit;

namespace Nordiska.FrontendApi.IntegrationTests.Banking;

public class AccountTypesControllerIntegrationTests : IClassFixture<CustomAuthWebApplicationFactory>
{
    private readonly CustomAuthWebApplicationFactory _factory;

    public AccountTypesControllerIntegrationTests(CustomAuthWebApplicationFactory factory)
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
    public async Task GetAll_Returns_AllStandardAccountTypes()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/account-types");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var types = await response.Content.ReadFromJsonAsync<List<AccountTypeConfigResponse>>();

        types.Should().NotBeNull();
        types!.Should().Contain(x => x.AccountType == "flex" && x.InterestRate == 0.0350m);
        types!.Should().Contain(x => x.AccountType == "fix" && x.InterestRate == 0.0410m);
        types!.Should().Contain(x => x.AccountType == "standard" && x.InterestRate == 0.0250m);
        types!.Should().Contain(x => x.AccountType == "saving" && x.InterestRate == 0.0350m);
        types!.Should().Contain(x => x.AccountType == "premium" && x.InterestRate == 0.0400m);
    }

    [Fact]
    public async Task GetByType_Returns_SpecificAccountType()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/account-types/flex");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var type = await response.Content.ReadFromJsonAsync<AccountTypeConfigResponse>();

        type.Should().NotBeNull();
        type!.AccountType.Should().Be("flex");
        type.InterestRate.Should().Be(0.0350m);
    }

    [Fact]
    public async Task OpenAccount_WithoutInterestRate_AutomaticallyAssignsConfiguredRate()
    {
        var client = await CreateAuthenticatedClientAsync("anna@exempel.se");
        var request = new OpenSavingsAccountRequest(
            CustomerId: 1,
            AccountType: "flex",
            InitialDeposit: 500m,
            AccountName: "Auto Rate Test Account"
        );

        var response = await client.PostAsJsonAsync("/api/accounts", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await response.Content.ReadFromJsonAsync<SavingsAccountResponse>();
        created.Should().NotBeNull();
        created!.AccountType.Should().Be("flex");
        created.InterestRate.Should().Be(0.0350m);
        created.Balance.Should().Be(500m);
    }
}
