namespace Nordiska.Modules.Inbox.Contracts.Responses;

/// <summary>
/// Representation of unread message thread count for customer inbox badge.
/// </summary>
/// <param name="UnreadCount">Number of unread message threads in the inbox folder.</param>
public sealed record UnreadCountResponse(int UnreadCount);
