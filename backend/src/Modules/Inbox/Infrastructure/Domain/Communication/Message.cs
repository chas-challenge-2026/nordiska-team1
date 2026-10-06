namespace Nordiska.Modules.Communication.Domain;
public sealed class Message
{
    public long Id { get; private set; }
    public long ThreadId { get; private set; }
    public MessageSenderType SenderType { get; private set; }
    public long? SenderCustomerId { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public bool ReplyAllowed { get; private set; }
    public DateTimeOffset SentAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    private Message() { }
    public Message(long threadId,MessageSenderType senderType,string body,bool replyAllowed,long? senderCustomerId=null)
    {ThreadId=threadId; SenderType=senderType; Body=body; ReplyAllowed=replyAllowed; SenderCustomerId=senderCustomerId; SentAt=DateTimeOffset.UtcNow;}
    public void Revoke()=>RevokedAt ??= DateTimeOffset.UtcNow;
}
public enum MessageSenderType { Bank=1, Customer=2, System=3 }
