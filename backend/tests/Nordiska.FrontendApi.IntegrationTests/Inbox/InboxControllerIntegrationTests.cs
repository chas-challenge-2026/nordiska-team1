using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Nordiska.BuildingBlocks.Database;
using Nordiska.FrontendApi.IntegrationTests.Postgres;
using Nordiska.Modules.Banking.Domain;
using Nordiska.Modules.Inbox.Contracts.Requests;
using Nordiska.Modules.Inbox.Contracts.Responses;
using Nordiska.Modules.Inbox.Infrastructure;
using Nordiska.Modules.Inbox.Infrastructure.Db;

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

    [PostgresFact]
    public async Task MarkFeedItemRead_ExistingCustomerThread_IsIdempotent()
    {
        var (client, _) = await CreateLoggedInCustomerAsync();
        var createResponse = await client.PostAsJsonAsync(
            "/api/inbox/threads",
            new CreateThreadRequest("Read endpoint", "Initial message", "Allmänt"));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var thread = await createResponse.Content.ReadFromJsonAsync<ThreadDetailResponse>();
        thread.Should().NotBeNull();

        var first = await client.PatchAsync($"/api/inbox/feed/thread-{thread!.Id}/read", null);
        var second = await client.PatchAsync($"/api/inbox/feed/thread-{thread.Id}/read", null);

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [PostgresFact]
    public async Task MarkFeedItemRead_OtherCustomersItem_ReturnsSameNotFoundAsUnknownItem()
    {
        var (firstClient, _) = await CreateLoggedInCustomerAsync();
        var (secondClient, _) = await CreateLoggedInCustomerAsync();
        var createResponse = await secondClient.PostAsJsonAsync(
            "/api/inbox/threads",
            new CreateThreadRequest("Private thread", "Private message", "Allmänt"));
        var thread = await createResponse.Content.ReadFromJsonAsync<ThreadDetailResponse>();

        var foreign = await firstClient.PatchAsync($"/api/inbox/feed/thread-{thread!.Id}/read", null);
        var unknown = await firstClient.PatchAsync("/api/inbox/feed/thread-9223372036854775807/read", null);

        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [PostgresFact]
    public async Task GetFeed_UnknownType_ReturnsBadRequest()
    {
        var (client, _) = await CreateLoggedInCustomerAsync();

        var response = await client.GetAsync("/api/inbox/feed?type=something-else");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [PostgresFact]
    public async Task AddNotification_WhenFeedProjectionSaveFails_RollsBackSourceNotification()
    {
        var (_, customer) = await CreateLoggedInCustomerAsync();
        var title = $"Atomic notification {Guid.NewGuid():N}";

        string connectionString;
        using (var scope = _factory.Services.CreateScope())
        {
            var registeredDb = scope.ServiceProvider.GetRequiredService<InboxDbContext>();
            connectionString = registeredDb.Database.GetConnectionString()!;
        }

        var failingOptions = new DbContextOptionsBuilder<InboxDbContext>()
            .UseNpgsql(connectionString)
            .AddInterceptors(new ThrowOnSecondSaveChangesInterceptor())
            .Options;

        await using (var failingDb = new InboxDbContext(failingOptions))
        {
            var repository = new InboxRepository(failingDb);
            var action = () => repository.AddNotificationAsync(customer.Id, "atomic-test", title);

            await action.Should().ThrowAsync<InvalidOperationException>();
        }

        var verifyOptions = new DbContextOptionsBuilder<InboxDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var verifyDb = new InboxDbContext(verifyOptions);

        (await verifyDb.CustomerNotifications.AnyAsync(x => x.CustomerId == customer.Id && x.Title == title))
            .Should().BeFalse();
        (await verifyDb.FeedItems.AnyAsync(x => x.CustomerId == customer.Id && x.Title == title))
            .Should().BeFalse();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await DeleteInboxDataAsync();
        await PostgresTestData.DeleteCustomersAsync(_factory.Services, _createdCustomerIds);
    }

    private async Task<(HttpClient Client, Customer Customer)> CreateLoggedInCustomerAsync()
    {
        var customer = await PostgresTestData.CreateCustomerAsync(_factory.Services, "inbox");
        _createdCustomerIds.Add(customer.Id);

        var client = await PostgresTestData.CreateLoggedInClientAsync(_factory, customer);
        return (client, customer);
    }

    private async Task DeleteInboxDataAsync()
    {
        if (_createdCustomerIds.Count == 0)
        {
            return;
        }

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InboxDbContext>();
        var threadIds = await (from state in db.MessageThreadStates
                               where _createdCustomerIds.Contains(state.CustomerId)
                               select state.ThreadId)
            .Distinct()
            .ToListAsync();

        await db.Messages.Where(x => threadIds.Contains(x.ThreadId)).ExecuteDeleteAsync();
        await db.MessageThreadStates.Where(x => _createdCustomerIds.Contains(x.CustomerId)).ExecuteDeleteAsync();
        await db.MessageThreads.Where(x => threadIds.Contains(x.Id)).ExecuteDeleteAsync();
        await db.FeedItems.Where(x => _createdCustomerIds.Contains(x.CustomerId)).ExecuteDeleteAsync();
        await db.CustomerNotifications.Where(x => _createdCustomerIds.Contains(x.CustomerId)).ExecuteDeleteAsync();
        await db.TermAcceptances.Where(x => _createdCustomerIds.Contains(x.CustomerId)).ExecuteDeleteAsync();
        await db.CustomerDocuments.Where(x => _createdCustomerIds.Contains(x.CustomerId)).ExecuteDeleteAsync();
        await db.MessageBoxes.Where(x => _createdCustomerIds.Contains(x.CustomerId)).ExecuteDeleteAsync();
    }

    private sealed class ThrowOnSecondSaveChangesInterceptor : SaveChangesInterceptor
    {
        private int _saveCount;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            _saveCount++;
            if (_saveCount == 2)
            {
                throw new InvalidOperationException("Simulated feed projection failure.");
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
