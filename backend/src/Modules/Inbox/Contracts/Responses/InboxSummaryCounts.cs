namespace Nordiska.Modules.Inbox.Contracts.Responses;

/// <summary>
/// Summary counts of unread and action-required items across the entire inbox.
/// </summary>
/// <param name="TotalUnread">Sum of <c>UnreadThreads</c>, <c>UnreadNotifications</c>, <c>UnreadDocuments</c>, and <c>UnreadTerms</c>. Minimum 0.</param>
/// <param name="UnreadThreads">Thread feed items whose inbox read timestamp is null. Minimum 0.</param>
/// <param name="UnreadNotifications">Standalone notification feed items whose inbox read timestamp is null. Minimum 0.</param>
/// <param name="UnopenedDocuments">Documents whose PDF/open action has never been recorded. This is domain state and can differ from <c>UnreadDocuments</c>. Minimum 0.</param>
/// <param name="PendingTerms">Terms still awaiting digital acceptance. This is domain state and can differ from <c>UnreadTerms</c>. Minimum 0.</param>
/// <param name="UnreadDocuments">Document feed items whose inbox read timestamp is null. Minimum 0.</param>
/// <param name="UnreadTerms">Term feed items whose inbox read timestamp is null. Minimum 0.</param>
/// <param name="ActionRequired">Feed items that still require a domain action, regardless of read state. Minimum 0.</param>
public sealed record InboxSummaryCounts(
    int TotalUnread,
    int UnreadThreads,
    int UnreadNotifications,
    int UnopenedDocuments,
    int PendingTerms,
    int UnreadDocuments = 0,
    int UnreadTerms = 0,
    int ActionRequired = 0);
