using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Nordiska.FrontendApi.Contracts.Requests;
using Nordiska.FrontendApi.IntegrationTests.Postgres;
using Nordiska.Modules.Banking.Domain;

namespace Nordiska.FrontendApi.IntegrationTests.Banking;

// Real Postgres, no fakes: updates a customer profile through the API and checks the row in the identity store.
[Collection(PostgresCollection.Name)]
public class CustomerProfileIntegrationTests : IAsyncLifetime
{
    private readonly PostgresAuthWebApplicationFactory _factory;
    private readonly List<long> _createdCustomerIds = new();

    public CustomerProfileIntegrationTests(PostgresAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [PostgresFact]
    public async Task Update_PersistsChangesToDatabase()
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var (client, customer) = await CreateLoggedInCustomerAsync();

        var response = await client.PutAsJsonAsync($"/api/customers/{customer.Id}", new UpdateCustomerRequest(
            Id: customer.Id,
            Name: $"Uppdaterad Kund {uniqueId}",
            Email: $"uppdaterad_{uniqueId}@exempel.se",
            PhoneNumber: "0701234567"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<CustomerResponse>();
        updated!.Name.Should().Be($"Uppdaterad Kund {uniqueId}");

        var stored = await GetStoredCustomerAsync(customer.Id);
        stored.Should().NotBeNull();
        stored!.Name.Should().Be($"Uppdaterad Kund {uniqueId}");
        stored.Email.Should().Be($"uppdaterad_{uniqueId}@exempel.se");
        stored.PhoneNumber.Should().Be("0701234567");
    }

    [PostgresFact]
    public async Task Update_WithoutIdInRouteOrBody_Returns400BadRequest()
    {
        var (client, _) = await CreateLoggedInCustomerAsync();

        var response = await client.PutAsJsonAsync("/api/customers", new { name = "Kund utan id" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [PostgresFact]
    public async Task GetById_ReturnsEmptyOverviewPreferenceByDefault()
    {
        var (client, customer) = await CreateLoggedInCustomerAsync();

        var response = await client.GetAsync($"/api/customers/{customer.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CustomerResponse>();
        body.Should().NotBeNull();
        body!.OverviewPreference.Should().NotBeNull();
        body.OverviewPreference.Should().BeEmpty();
    }

    [PostgresFact]
    public async Task Patch_WithOverviewPreference_PersistsAndReturnsUpdatedPreferences()
    {
        var (client, customer) = await CreateLoggedInCustomerAsync();
        var preferences = new List<string> { "accounts", "savings", "transactions", "inbox" };

        var patchResponse = await client.PatchAsJsonAsync($"/api/customers/{customer.Id}", new PatchCustomerRequest(
            OverviewPreference: preferences));

        patchResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var patchBody = await patchResponse.Content.ReadFromJsonAsync<CustomerResponse>();
        patchBody.Should().NotBeNull();
        patchBody!.OverviewPreference.Should().BeEquivalentTo(preferences);

        // Verify via GET
        var getResponse = await client.GetAsync($"/api/customers/{customer.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var getBody = await getResponse.Content.ReadFromJsonAsync<CustomerResponse>();
        getBody.Should().NotBeNull();
        getBody!.OverviewPreference.Should().BeEquivalentTo(preferences);

        // Verify in database
        var stored = await GetStoredCustomerAsync(customer.Id);
        stored.Should().NotBeNull();
        stored!.OverviewPreference.Should().BeEquivalentTo(preferences);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => PostgresTestData.DeleteCustomersAsync(_factory.Services, _createdCustomerIds);

    private async Task<(HttpClient Client, Customer Customer)> CreateLoggedInCustomerAsync()
    {
        var customer = await PostgresTestData.CreateCustomerAsync(_factory.Services, "profile");
        _createdCustomerIds.Add(customer.Id);

        var client = await PostgresTestData.CreateLoggedInClientAsync(_factory, customer);
        return (client, customer);
    }

    private async Task<Customer?> GetStoredCustomerAsync(long customerId)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Customer>>();
        return await userManager.FindByIdAsync(customerId.ToString());
    }
}
