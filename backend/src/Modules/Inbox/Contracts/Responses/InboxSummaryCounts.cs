namespace Nordiska.Modules.Inbox.Contracts.Responses;

/// <summary>
/// Summary counts of unread and action-required items across the entire inbox.
/// </summary>
/// <param name="TotalUnread">Grand total of all unread items across threads, notifications, documents, and pending terms.</param>
/// <param name="UnreadThreads">Count of unread message threads in the inbox folder.</param>
/// <param name="UnreadNotifications">Count of unread customer notifications.</param>
/// <param name="UnopenedDocuments">Count of archived documents that have not yet been opened.</param>
/// <param name="PendingTerms">Count of pending terms and conditions requiring digital acceptance.</param>
public sealed record InboxSummaryCounts(
    int TotalUnread,
    int UnreadThreads,
    int UnreadNotifications,
    int UnopenedDocuments,
    int PendingTerms);