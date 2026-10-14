namespace Nordiska.Modules.Communication.Domain;

public sealed class MessageThread
{
    public long Id { get; private set; }
    public long MessageBoxId { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public MessageThreadStatus Status { get; private set; }
    public bool IsInformationOnly { get; private set; }
    public string? Category { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset LastMessageAt { get; private set; }

    private MessageThread() { }

    public MessageThread(
        long messageBoxId,
        string subject,
        bool isInformationOnly = false,
        string? category = null)
    {
        MessageBoxId = messageBoxId;
        Subject = subject;
        Status = MessageThreadStatus.Open;
        IsInformationOnly = isInformationOnly;
        Category = category;
        CreatedAt = LastMessageAt = DateTimeOffset.UtcNow;
    }

    public void RegisterMessage(DateTimeOffset sentAt) => LastMessageAt = sentAt;
    public void Close() => Status = MessageThreadStatus.Closed;
    public void Reopen() => Status = MessageThreadStatus.Open;
}

public enum MessageThreadStatus { Open = 1, Closed = 2 }

