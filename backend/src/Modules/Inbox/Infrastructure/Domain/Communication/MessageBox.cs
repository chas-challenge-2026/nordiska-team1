namespace Nordiska.Modules.Communication.Domain;
public sealed class MessageBox
{
    public long Id { get; private set; }
    public long CustomerId { get; private set; }
    public MessageBoxType Type { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    private MessageBox() { }
    public MessageBox(long customerId){CustomerId=customerId; Type=MessageBoxType.Personal; CreatedAt=DateTimeOffset.UtcNow;}
}
public enum MessageBoxType { Personal=1 }
