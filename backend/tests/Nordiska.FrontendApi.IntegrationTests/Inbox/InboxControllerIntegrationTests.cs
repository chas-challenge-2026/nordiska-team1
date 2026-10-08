using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Nordiska.BuildingBlocks.Database;
using Nordiska.FrontendApi.IntegrationTests.Postgres;
using Nordiska.Modules.Banking.Domain;
using Nordiska.Modules.Inbox.Contracts.Responses;

namespace Nordiska.FrontendApi.IntegrationTests.Inbox;

[Collection(PostgresCollection.Name)]
public class InboxControllerIntegrationTests : IAsyncLifetime
{
    private readonly PostgresAuthWebApplicationFactory _factory;
    private readonly List<long> _createdCustomerIds = new();

    public InboxControllerIntegrationTests(PostgresAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [PostgresFact]
    public async Task GetUnreadCount_ReturnsOk_WithTotalAndBreakdown()
    {
        var (client, _) = await CreateLoggedInCustomerAsync();

        var response = await client.GetAsync("/api/inbox/unread-count");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UnreadCountResponse>();
        body.Should().NotBeNull();
        body!.UnreadCount.Should().BeGreaterThanOrEqualTo(0);
        body.TotalUnread.Should().BeGreaterThanOrEqualTo(0);
        body.UnreadThreads.Should().BeGreaterThanOrEqualTo(0);
    }

    [PostgresFact]
    public async Task GetOverview_ReturnsOk_WithCountsAndFeed()
    {
        var (client, _) = await CreateLoggedInCustomerAsync();

        var response = await client.GetAsync("/api/inbox/overview");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<InboxOverviewResponse>();
        body.Should().NotBeNull();
        body!.Counts.Should().NotBeNull();
        body.Feed.Should().NotBeNull();
        body.UnreadFeed.Should().NotBeNull();
        body.PendingTerms.Should().NotBeNull();
    }

    [PostgresFact]
    public async Task GetGeneralDocuments_ReturnsOk_WithDocumentList()
    {
        var (client, _) = await CreateLoggedInCustomerAsync();

        var response = await client.GetAsync("/api/inbox/documents/general");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IReadOnlyList<GeneralDocumentResponse>>();
        body.Should().NotBeNull();
    }

    [PostgresFact]
    public async Task GetFeed_ReturnsOk_WithPagedResult()
    {
        var (client, _) = await CreateLoggedInCustomerAsync();

        var response = await client.GetAsync("/api/inbox/feed?page=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResult<InboxFeedItemResponse>>();
        body.Should().NotBeNull();
        body!.Page.Should().Be(1);
        body.PageSize.Should().Be(10);
    }

    [PostgresFact]
    public async Task MarkAllAsRead_ReturnsOk_WithConfirmation()
    {
        var (client, _) = await CreateLoggedInCustomerAsync();

        var response = await client.PostAsync("/api/inbox/read-all", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<MarkAllReadResponse>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => PostgresTestData.DeleteCustomersAsync(_factory.Services, _createdCustomerIds);

    private async Task<(HttpClient Client, Customer Customer)> CreateLoggedInCustomerAsync()
    {
        var customer = await PostgresTestData.CreateCustomerAsync(_factory.Services, "inbox");
        _createdCustomerIds.Add(customer.Id);

        var client = await PostgresTestData.CreateLoggedInClientAsync(_factory, customer);
        return (client, customer);
    }
}