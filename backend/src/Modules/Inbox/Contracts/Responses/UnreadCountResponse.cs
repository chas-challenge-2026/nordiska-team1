namespace Nordiska.Modules.Inbox.Contracts.Responses;

/// <summary>
/// Representation of unread counts for customer inbox badges and summary indicators.
/// </summary>
/// <param name="UnreadCount">Total count of unread items across all categories for backwards compatibility.</param>
/// <param name="TotalUnread">Grand total of all unread items (threads, notifications, unopened documents, pending terms).</param>
/// <param name="UnreadThreads">Count of unread message threads in inbox folder.</param>
/// <param name="UnreadNotifications">Count of unread customer notifications.</param>
/// <param name="UnopenedDocuments">Count of unopened archived documents.</param>
/// <param name="PendingTerms">Count of pending terms requiring customer acceptance.</param>
public sealed record UnreadCountResponse(
    int UnreadCount,
    int TotalUnread = 0,
    int UnreadThreads = 0,
    int UnreadNotifications = 0,
    int UnopenedDocuments = 0,
    int PendingTerms = 0);