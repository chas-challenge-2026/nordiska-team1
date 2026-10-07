namespace Nordiska.Modules.Inbox.Contracts.Responses;

/// <summary>
/// Summary view of a message thread for folder listing.
/// </summary>
/// <param name="Id">Unique thread/case ID.</param>
/// <param name="Subject">Subject of the thread.</param>
/// <param name="Status">Thread status ('Open', 'Closed').</param>
/// <param name="CreatedAt">Thread creation timestamp.</param>
/// <param name="LastMessageAt">Timestamp of the most recent message in the thread.</param>
/// <param name="IsRead">Whether the current customer has read the latest messages in this thread.</param>
/// <param name="Folder">Current folder for this customer ('Inbox', 'Sent', 'Archive').</param>
/// <param name="MessageCount">Total number of messages in the thread.</param>
/// <param name="IsInformationOnly">Whether this is a one-way announcement/information message.</param>
/// <param name="CanReply">Whether replies are permitted on this thread.</param>
/// <param name="Category">Optional category label.</param>
public sealed record ThreadSummaryResponse(
    long Id,
    string Subject,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastMessageAt,
    bool IsRead,
    string Folder,
    int MessageCount,
    bool IsInformationOnly = false,
    bool CanReply = true,
    string? Category = null);
