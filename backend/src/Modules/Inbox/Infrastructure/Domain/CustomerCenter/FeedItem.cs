namespace Nordiska.Modules.CustomerCenter.Domain;
public sealed class FeedItem
{
    public long Id { get; private set; }
    public long CustomerId { get; private set; }
    public FeedItemType ItemType { get; private set; }
    public long SourceId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Preview { get; private set; }
    public FeedPriority Priority { get; private set; }
    public bool ActionRequired { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }
    private FeedItem() { }
    public FeedItem(long customerId,FeedItemType itemType,long sourceId,string title,string? preview,FeedPriority priority,bool actionRequired,DateTimeOffset occurredAt)
    {CustomerId=customerId; ItemType=itemType; SourceId=sourceId; Title=title; Preview=preview; Priority=priority; ActionRequired=actionRequired; OccurredAt=occurredAt;}
    public bool IsRead=>ReadAt is not null;
    public void MarkAsRead()=>ReadAt ??= DateTimeOffset.UtcNow;
    public void MarkAsUnread()=>ReadAt = null;
    public void Update(string title,string? preview,FeedPriority priority,bool actionRequired,DateTimeOffset occurredAt)
    {Title=title; Preview=preview; Priority=priority; ActionRequired=actionRequired; OccurredAt=occurredAt;}
}
public enum FeedItemType { Message=1, Document=2, Terms=3, Loan=4, Notification=5 }
public enum FeedPriority { Normal=1, Important=2, Critical=3 }
