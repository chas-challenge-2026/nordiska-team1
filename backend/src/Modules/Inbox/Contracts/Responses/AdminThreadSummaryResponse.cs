namespace Nordiska.Modules.Inbox.Contracts.Responses;

/// <summary>
/// Admin summary representation of a support thread.
/// </summary>
/// <param name="Id">Thread ID.</param>
/// <param name="CustomerId">Customer ID.</param>
/// <param name="CustomerName">Customer display name.</param>
/// <param name="Subject">Subject of the thread.</param>
/// <param name="Status">Thread status ('Open', 'Closed').</param>
/// <param name="Category">Category label.</param>
/// <param name="IsInformationOnly">Whether this is a broadcast / information message.</param>
/// <param name="CanReply">Whether replies are enabled.</param>
/// <param name="MessageCount">Total number of messages in the thread.</param>
/// <param name="CreatedAt">Thread creation timestamp.</param>
/// <param name="LastMessageAt">Timestamp of latest message.</param>
public sealed record AdminThreadSummaryResponse(
    long Id,
    long CustomerId,
    string CustomerName,
    string Subject,
    string Status,
    string? Category,
    bool IsInformationOnly,
    bool CanReply,
    int MessageCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastMessageAt);