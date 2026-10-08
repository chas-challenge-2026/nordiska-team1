namespace Nordiska.Modules.Inbox.Contracts.Responses;

/// <summary>
/// Comprehensive inbox dashboard response designed for the primary Inbox UI view.
/// Provides unread summary counts, a unified chronological feed of recent events, dedicated unread feed, and urgent pending terms.
/// </summary>
/// <param name="Counts">Summary of unread counts across all inbox categories.</param>
/// <param name="Feed">Recent items in the unified chronological feed (both read and unread).</param>
/// <param name="UnreadFeed">Items in the unified feed requiring customer attention or marked unread.</param>
/// <param name="PendingTerms">Terms and conditions awaiting customer acceptance.</param>
public sealed record InboxOverviewResponse(
    InboxSummaryCounts Counts,
    IReadOnlyList<InboxFeedItemResponse> Feed,
    IReadOnlyList<InboxFeedItemResponse> UnreadFeed,
    IReadOnlyList<PendingTermResponse> PendingTerms);