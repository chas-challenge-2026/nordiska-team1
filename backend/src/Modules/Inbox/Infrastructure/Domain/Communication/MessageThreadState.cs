namespace Nordiska.Modules.Communication.Domain;
public sealed class MessageThreadState
{
    public long Id { get; private set; }
    public long ThreadId { get; private set; }
    public long CustomerId { get; private set; }
    public MessageFolder Folder { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }
    private MessageThreadState() { }
    public MessageThreadState(long threadId,long customerId,MessageFolder folder){ThreadId=threadId; CustomerId=customerId; Folder=folder;}
    public bool IsRead=>ReadAt is not null;
    public void MarkAsRead()=>ReadAt ??= DateTimeOffset.UtcNow;
    public void MarkAsUnread()=>ReadAt = null;
    public void Archive(){Folder=MessageFolder.Archive; ArchivedAt=DateTimeOffset.UtcNow;}
    public void MoveToInbox(){Folder=MessageFolder.Inbox; ArchivedAt=null;}
}
public enum MessageFolder { Inbox=1, Sent=2, Archive=3 }
