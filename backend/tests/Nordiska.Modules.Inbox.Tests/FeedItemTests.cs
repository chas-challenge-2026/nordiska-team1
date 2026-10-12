using Nordiska.Modules.CustomerCenter.Domain;

namespace Nordiska.Modules.Inbox.Tests;

public sealed class FeedItemTests
{
    [Fact]
    public void MarkAsRead_CalledTwice_PreservesFirstReadTimestamp()
    {
        var item = CreateItem();

        item.MarkAsRead();
        var firstReadAt = item.ReadAt;
        item.MarkAsRead();

        Assert.NotNull(firstReadAt);
        Assert.Equal(firstReadAt, item.ReadAt);
    }

    [Fact]
    public void MarkAsUnread_ReadItem_ClearsReadTimestamp()
    {
        var item = CreateItem();
        item.MarkAsRead();

        item.MarkAsUnread();

        Assert.False(item.IsRead);
        Assert.Null(item.ReadAt);
    }

    [Fact]
    public void Update_ReadItem_ChangesProjectionDataWithoutLosingIdentityOrReadState()
    {
        var item = CreateItem();
        item.MarkAsRead();
        var readAt = item.ReadAt;
        var occurredAt = new DateTimeOffset(2026, 10, 8, 12, 30, 0, TimeSpan.Zero);

        item.Update(
            "Updated title",
            "Updated preview",
            FeedPriority.Critical,
            actionRequired: true,
            occurredAt);

        Assert.Equal(41, item.CustomerId);
        Assert.Equal(FeedItemType.Message, item.ItemType);
        Assert.Equal(73, item.SourceId);
        Assert.Equal("Updated title", item.Title);
        Assert.Equal("Updated preview", item.Preview);
        Assert.Equal(FeedPriority.Critical, item.Priority);
        Assert.True(item.ActionRequired);
        Assert.Equal(occurredAt, item.OccurredAt);
        Assert.Equal(readAt, item.ReadAt);
    }

    [Fact]
    public void Update_ReappliedWithSameValues_RemainsStable()
    {
        var occurredAt = new DateTimeOffset(2026, 10, 8, 12, 30, 0, TimeSpan.Zero);
        var item = CreateItem();

        item.Update("Updated", "Preview", FeedPriority.Important, false, occurredAt);
        item.Update("Updated", "Preview", FeedPriority.Important, false, occurredAt);

        Assert.Equal("Updated", item.Title);
        Assert.Equal("Preview", item.Preview);
        Assert.Equal(FeedPriority.Important, item.Priority);
        Assert.False(item.ActionRequired);
        Assert.Equal(occurredAt, item.OccurredAt);
    }

    private static FeedItem CreateItem() => new(
        customerId: 41,
        itemType: FeedItemType.Message,
        sourceId: 73,
        title: "Original title",
        preview: "Original preview",
        priority: FeedPriority.Normal,
        actionRequired: false,
        occurredAt: new DateTimeOffset(2026, 10, 8, 10, 0, 0, TimeSpan.Zero));
}
