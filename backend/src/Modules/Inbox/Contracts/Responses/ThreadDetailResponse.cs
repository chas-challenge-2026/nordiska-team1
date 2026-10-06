namespace Nordiska.Modules.Inbox.Contracts.Responses;

/// <summary>
/// Detailed view of a message thread including all messages.
/// </summary>
/// <param name="Id">Unique thread/case ID.</param>
/// <param name="Subject">Subject of the thread.</param>
/// <param name="Status">Thread status ('Open', 'Closed').</param>
/// <param name="CreatedAt">Thread creation timestamp.</param>
/// <param name="LastMessageAt">Timestamp of the most recent message in the thread.</param>
/// <param name="IsRead">Whether the thread is read.</param>
/// <param name="Folder">Current folder for this customer ('Inbox', 'Sent', 'Archive').</param>
/// <param name="CanReply">Whether customer is permitted to reply to the thread (Status is Open and last message allows reply).</param>
/// <param name="Messages">List of all messages in the thread ordered chronologically.</param>
public sealed record ThreadDetailResponse(
    long Id,
    string Subject,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastMessageAt,
    bool IsRead,
    string Folder,
    bool CanReply,
    IReadOnlyList<MessageResponse> Messages);
