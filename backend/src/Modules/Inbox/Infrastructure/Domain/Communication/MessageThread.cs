namespace Nordiska.Modules.Communication.Domain;
public sealed class MessageThread
{
    public long Id { get; private set; }
    public long MessageBoxId { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public MessageThreadStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset LastMessageAt { get; private set; }
    private MessageThread() { }
    public MessageThread(long messageBoxId,string subject){MessageBoxId=messageBoxId; Subject=subject; Status=MessageThreadStatus.Open; CreatedAt=LastMessageAt=DateTimeOffset.UtcNow;}
    public void RegisterMessage(DateTimeOffset sentAt)=>LastMessageAt=sentAt;
    public void Close()=>Status=MessageThreadStatus.Closed;
}
public enum MessageThreadStatus { Open=1, Closed=2 }
