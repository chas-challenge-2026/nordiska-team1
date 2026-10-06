namespace Nordiska.Modules.Communication.Domain;
public sealed class CustomerNotification
{
    public long Id { get; private set; }
    public long CustomerId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public NotificationPriority Priority { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Body { get; private set; }
    public NotificationTargetType? TargetType { get; private set; }
    public long? TargetId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    private CustomerNotification() { }
    public CustomerNotification(long customerId,string type,string title,string? body=null,NotificationPriority priority=NotificationPriority.Normal,NotificationTargetType? targetType=null,long? targetId=null,DateTimeOffset? expiresAt=null)
    {CustomerId=customerId; Type=type; Title=title; Body=body; Priority=priority; TargetType=targetType; TargetId=targetId; ExpiresAt=expiresAt; CreatedAt=DateTimeOffset.UtcNow;}
    public bool IsRead=>ReadAt is not null;
    public void MarkAsRead()=>ReadAt ??= DateTimeOffset.UtcNow;
}
public enum NotificationPriority { Low=1, Normal=2, High=3, Critical=4 }
public enum NotificationTargetType { Document=1, MessageThread=2, Term=3, Loan=4 }
