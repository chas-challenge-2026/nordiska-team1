using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Nordiska.BuildingBlocks.Database;
using Nordiska.FrontendApi.Contracts.Requests;
using Nordiska.FrontendApi.IntegrationTests.Authentication;
using Nordiska.Modules.Banking.Contracts.Responses;
using Xunit;

namespace Nordiska.FrontendApi.IntegrationTests.Banking;

public class TransactionsPaginationIntegrationTests : IClassFixture<CustomAuthWebApplicationFactory>
{
    private readonly CustomAuthWebApplicationFactory _factory;

    public TransactionsPaginationIntegrationTests(CustomAuthWebApplicationFactory factory)
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
    public async Task GetAll_WithoutToken_Returns_401Unauthorized()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/transactions");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAll_WithAuthenticatedUser_Returns_PagedResult()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/transactions?page=1&pageSize=10");
        var content = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: content);
        using var doc = JsonDocument.Parse(content);

        doc.RootElement.TryGetProperty("items", out var itemsElement).Should().BeTrue();
        doc.RootElement.TryGetProperty("totalCount", out var totalCountElement).Should().BeTrue();
        doc.RootElement.TryGetProperty("page", out var pageElement).Should().BeTrue();
        doc.RootElement.TryGetProperty("pageSize", out var pageSizeElement).Should().BeTrue();

        pageElement.GetInt32().Should().Be(1);
        pageSizeElement.GetInt32().Should().Be(10);
    }

    [Fact]
    public async Task GetAll_FilteredByNonExistentOrUnownedAccount_Returns_403Forbidden()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/transactions?accountId=999999");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
