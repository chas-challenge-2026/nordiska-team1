using Microsoft.EntityFrameworkCore;
using Nordiska.Modules.Agreements.Domain;
using Nordiska.Modules.Communication.Domain;
using Nordiska.Modules.CustomerCenter.Domain;
using Nordiska.Modules.Documents.Domain;
using Nordiska.Modules.Inbox.Application;
using Nordiska.Modules.Inbox.Infrastructure;
using Nordiska.Modules.Inbox.Infrastructure.Db;

namespace Nordiska.FrontendApi.IntegrationTests.Inbox;

public sealed class InboxFeedProjectionIntegrationTests
{
    [Fact]
    public async Task CreateCustomerThread_CreatesReadCanonicalFeedItem()
    {
        await using var db = CreateContext();
        var repository = new InboxRepository(db);

        var thread = await repository.CreateThreadWithInitialMessageAsync(
            41,
            "Support question",
            "Initial message");

        var item = await db.FeedItems.SingleAsync();
        Assert.Equal(41, item.CustomerId);
        Assert.Equal(FeedItemType.Message, item.ItemType);
        Assert.Equal(thread.Id, item.SourceId);
        Assert.Equal("Support question", item.Title);
        Assert.Equal("Initial message", item.Preview);
        Assert.True(item.IsRead);
    }

    [Fact]
    public async Task AddNotifications_ProjectsStandaloneButNotThreadTarget()
    {
        await using var db = CreateContext();
        var repository = new InboxRepository(db);

        var standalone = await repository.AddNotificationAsync(
            41,
            "system",
            "Standalone",
            "Standalone body");
        await repository.AddNotificationAsync(
            41,
            "support_message",
            "Thread duplicate",
            "Thread body",
            targetType: NotificationTargetType.MessageThread,
            targetId: 73);

        var item = await db.FeedItems.SingleAsync();
        Assert.Equal(FeedItemType.Notification, item.ItemType);
        Assert.Equal(standalone.Id, item.SourceId);
        Assert.Equal("Standalone", item.Title);
    }

    [Fact]
    public async Task AddBankMessage_UpdatesThreadProjectionAndMarksItUnread()
    {
        await using var db = CreateContext();
        var repository = new InboxRepository(db);
        var thread = await repository.CreateThreadWithInitialMessageAsync(
            41,
            "Support question",
            "Initial message");

        await repository.AddMessageAsync(
            thread.Id,
            MessageSenderType.Bank,
            senderCustomerId: null,
            body: "Bank reply",
            replyAllowed: true);

        var item = await db.FeedItems.SingleAsync();
        Assert.Equal("Bank reply", item.Preview);
        Assert.False(item.IsRead);
        Assert.True(item.OccurredAt >= thread.CreatedAt);
    }

    [Fact]
    public async Task PublishAndAcceptTerm_UpdatesActionStateAndReadState()
    {
        await using var db = CreateContext();
        var repository = new InboxRepository(db);

        var term = await repository.PublishTermAsync(
            "privacy",
            2,
            "Privacy terms",
            documentId: 91,
            effectiveFrom: new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.Zero),
            targetCustomerIds: [41]);

        var pendingItem = await db.FeedItems.SingleAsync();
        Assert.Equal(FeedItemType.Terms, pendingItem.ItemType);
        Assert.Equal(term.Id, pendingItem.SourceId);
        Assert.True(pendingItem.ActionRequired);
        Assert.False(pendingItem.IsRead);

        var acceptance = await db.TermAcceptances.SingleAsync();
        await repository.AcceptTermAsync(acceptance);

        var acceptedItem = await db.FeedItems.SingleAsync();
        Assert.False(acceptedItem.ActionRequired);
        Assert.True(acceptedItem.IsRead);
        Assert.Equal("Version 2. Godkänd.", acceptedItem.Preview);
        Assert.Equal(TermAcceptanceStatus.Accepted, acceptance.Status);
    }

    [Fact]
    public async Task MarkDocumentOpened_MarksAuditAndFeedItemRead()
    {
        await using var db = CreateContext();
        var document = new Document(
            "TaxReport",
            "Tax report",
            "tax-report.pdf",
            "application/pdf",
            "storage/tax-report.pdf",
            123,
            new string('a', 64),
            "TaxReport",
            "91");
        db.Documents.Add(document);
        await db.SaveChangesAsync();

        var customerDocument = new CustomerDocument(document.Id, 41);
        db.CustomerDocuments.Add(customerDocument);
        await db.SaveChangesAsync();

        db.FeedItems.Add(new FeedItem(
            41,
            FeedItemType.Document,
            document.Id,
            document.Title,
            document.FileName,
            FeedPriority.Normal,
            false,
            customerDocument.PublishedAt));
        await db.SaveChangesAsync();

        var repository = new InboxRepository(db);
        await repository.MarkDocumentOpenedAsync(customerDocument.Id);

        Assert.True(customerDocument.HasBeenOpened);
        Assert.True((await db.FeedItems.SingleAsync()).IsRead);
    }

    [Fact]
    public async Task MarkAllFeedItemsRead_MarksEveryFeedTypeWithoutPerformingDomainActions()
    {
        await using var db = CreateContext();
        var repository = new InboxRepository(db);
        var thread = await repository.CreateThreadWithInitialMessageAsync(
            41,
            "Bank message",
            "Message body",
            replyAllowed: true,
            isInformationOnly: false,
            category: "Allmänt",
            senderType: MessageSenderType.Bank);
        var notification = await repository.AddNotificationAsync(41, "system", "Notification");

        var document = new Document(
            "TaxReport",
            "Tax report",
            "tax-report.pdf",
            "application/pdf",
            "storage/tax-report.pdf",
            123,
            new string('a', 64),
            "TaxReport");
        db.Documents.Add(document);
        await db.SaveChangesAsync();
        var customerDocument = new CustomerDocument(document.Id, 41);
        db.CustomerDocuments.Add(customerDocument);
        db.FeedItems.Add(new FeedItem(
            41,
            FeedItemType.Document,
            document.Id,
            document.Title,
            document.FileName,
            FeedPriority.Normal,
            false,
            customerDocument.PublishedAt));
        await db.SaveChangesAsync();

        var term = await repository.PublishTermAsync(
            "privacy",
            2,
            "Privacy terms",
            document.Id,
            new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.Zero),
            [41]);
        var loanItem = new FeedItem(
            41,
            FeedItemType.Loan,
            99,
            "Loan",
            null,
            FeedPriority.Normal,
            false,
            DateTimeOffset.UtcNow);
        db.FeedItems.Add(loanItem);
        await db.SaveChangesAsync();

        var counts = await repository.MarkAllFeedItemsReadAsync(41);

        Assert.Equal(new FeedReadCounts(1, 1, 1, 1), counts);
        Assert.Equal(4, counts.Total);
        Assert.All(
            await db.FeedItems.Where(x => x.ItemType != FeedItemType.Loan).ToListAsync(),
            item => Assert.True(item.IsRead));
        Assert.False(loanItem.IsRead);
        Assert.True((await db.MessageThreadStates.SingleAsync(x => x.ThreadId == thread.Id)).IsRead);
        Assert.True((await db.CustomerNotifications.SingleAsync(x => x.Id == notification.Id)).IsRead);
        Assert.False(customerDocument.HasBeenOpened);
        Assert.Equal(TermAcceptanceStatus.Pending, (await db.TermAcceptances.SingleAsync(x => x.TermId == term.Id)).Status);
    }

    private static InboxDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<InboxDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new InboxDbContext(options);
    }
}
