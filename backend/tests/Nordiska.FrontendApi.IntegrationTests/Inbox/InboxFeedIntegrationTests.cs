using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Nordiska.Modules.Communication.Domain;
using Nordiska.Modules.CustomerCenter.Domain;
using Nordiska.Modules.Inbox.Contracts.Requests;
using Nordiska.Modules.Inbox.Infrastructure;
using Nordiska.Modules.Inbox.Infrastructure.Db;

namespace Nordiska.FrontendApi.IntegrationTests.Inbox;

public sealed class InboxFeedIntegrationTests
{
    [Fact]
    public async Task GetUnifiedFeed_MoreThanOneHundredItems_ReturnsAccurateTotalAndSecondPage()
    {
        await using var db = CreateContext();
        var notifications = Enumerable.Range(1, 105)
            .Select(i => new CustomerNotification(41, "system", $"Notification {i:D3}"))
            .ToList();
        db.CustomerNotifications.AddRange(notifications);
        await db.SaveChangesAsync();

        var occurredAt = new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
        db.FeedItems.AddRange(notifications.Select((notification, index) => new FeedItem(
            41,
            FeedItemType.Notification,
            notification.Id,
            notification.Title,
            null,
            FeedPriority.Normal,
            false,
            occurredAt.AddMinutes(index))));
        await db.SaveChangesAsync();

        var repository = new InboxRepository(db);
        var result = await repository.GetUnifiedFeedAsync(
            41,
            new FeedQueryParameters(Type: "notification", Page: 2, PageSize: 20));
        var items = result.Items.ToList();

        Assert.Equal(105, result.TotalCount);
        Assert.Equal(20, items.Count);
        Assert.Equal("notification-85", items[0].Id);
        Assert.Equal("notification-66", items[^1].Id);
    }

    [Fact]
    public async Task GetUnifiedFeed_ReadPendingTerm_DoesNotAppearInUnreadOnly()
    {
        await using var db = CreateContext();
        var repository = new InboxRepository(db);
        var term = await repository.PublishTermAsync(
            "privacy",
            2,
            "Privacy terms",
            91,
            new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.Zero),
            [41]);
        await repository.MarkFeedItemReadAsync(41, FeedItemType.Terms, term.Id);

        var result = await repository.GetUnifiedFeedAsync(
            41,
            new FeedQueryParameters(UnreadOnly: true));

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(1, await db.TermAcceptances.CountAsync(x => x.Status == Nordiska.Modules.Agreements.Domain.TermAcceptanceStatus.Pending));
    }

    [Fact]
    public async Task GetUnifiedFeed_ThreadTargetNotification_ReturnsOnlyCanonicalThreadItem()
    {
        await using var db = CreateContext();
        var repository = new InboxRepository(db);
        var thread = await repository.CreateThreadWithInitialMessageAsync(
            41,
            "Bank message",
            "Message body",
            senderType: MessageSenderType.Bank,
            replyAllowed: true,
            isInformationOnly: false,
            category: "Allmänt");
        await repository.AddNotificationAsync(
            41,
            "bank_message",
            "Duplicate notification",
            targetType: NotificationTargetType.MessageThread,
            targetId: thread.Id);

        var result = await repository.GetUnifiedFeedAsync(41, new FeedQueryParameters());

        var item = Assert.Single(result.Items);
        Assert.Equal($"thread-{thread.Id}", item.Id);
        Assert.Single(await db.CustomerNotifications.ToListAsync());
    }

    [Fact]
    public async Task GetSummaryCounts_UsesFeedReadStateAndKeepsActionCountSeparate()
    {
        await using var db = CreateContext();
        var occurredAt = new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
        var thread = new FeedItem(41, FeedItemType.Message, 1, "Thread", null, FeedPriority.Normal, false, occurredAt);
        var notification = new FeedItem(41, FeedItemType.Notification, 2, "Notification", null, FeedPriority.Normal, false, occurredAt);
        var document = new FeedItem(41, FeedItemType.Document, 3, "Document", null, FeedPriority.Normal, false, occurredAt);
        var term = new FeedItem(41, FeedItemType.Terms, 4, "Term", null, FeedPriority.Important, true, occurredAt);
        document.MarkAsRead();
        db.FeedItems.AddRange(thread, notification, document, term);
        await db.SaveChangesAsync();

        var repository = new InboxRepository(db);
        var counts = await repository.GetSummaryCountsAsync(41);

        Assert.Equal(3, counts.TotalUnread);
        Assert.Equal(1, counts.UnreadThreads);
        Assert.Equal(1, counts.UnreadNotifications);
        Assert.Equal(0, counts.UnreadDocuments);
        Assert.Equal(1, counts.UnreadTerms);
        Assert.Equal(1, counts.ActionRequired);
    }

    [Fact]
    public async Task GetUnifiedFeed_UnknownType_ThrowsValidationException()
    {
        await using var db = CreateContext();
        var repository = new InboxRepository(db);

        await Assert.ThrowsAsync<ValidationException>(() =>
            repository.GetUnifiedFeedAsync(41, new FeedQueryParameters(Type: "something-else")));
    }

    private static InboxDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<InboxDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new InboxDbContext(options);
    }
}
