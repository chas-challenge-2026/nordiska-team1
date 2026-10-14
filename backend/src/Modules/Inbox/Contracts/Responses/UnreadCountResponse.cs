namespace Nordiska.Modules.Inbox.Contracts.Responses;

/// <summary>
/// Representation of unread counts for customer inbox badges and summary indicators.
/// </summary>
/// <param name="UnreadCount">Backwards-compatible alias of <c>TotalUnread</c>. Do not add it to <c>TotalUnread</c>. Minimum 0.</param>
/// <param name="TotalUnread">Sum of <c>UnreadThreads</c>, <c>UnreadNotifications</c>, <c>UnreadDocuments</c>, and <c>UnreadTerms</c>. Minimum 0.</param>
/// <param name="UnreadThreads">Thread feed items whose inbox read timestamp is null. Minimum 0.</param>
/// <param name="UnreadNotifications">Standalone notification feed items whose inbox read timestamp is null. Minimum 0.</param>
/// <param name="UnopenedDocuments">Documents whose PDF/open action has never been recorded. This can differ from <c>UnreadDocuments</c>. Minimum 0.</param>
/// <param name="PendingTerms">Terms still awaiting digital acceptance. This can differ from <c>UnreadTerms</c>. Minimum 0.</param>
/// <param name="UnreadDocuments">Document feed items whose inbox read timestamp is null. Minimum 0.</param>
/// <param name="UnreadTerms">Term feed items whose inbox read timestamp is null. Minimum 0.</param>
/// <param name="ActionRequired">Feed items that still require a separate domain action, regardless of read state. Minimum 0.</param>
public sealed record UnreadCountResponse(
    int UnreadCount,
    int TotalUnread = 0,
    int UnreadThreads = 0,
    int UnreadNotifications = 0,
    int UnopenedDocuments = 0,
    int PendingTerms = 0,
    int UnreadDocuments = 0,
    int UnreadTerms = 0,
    int ActionRequired = 0);
