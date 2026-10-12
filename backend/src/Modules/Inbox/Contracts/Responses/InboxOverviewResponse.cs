namespace Nordiska.Modules.Inbox.Contracts.Responses;

/// <summary>
/// Convenience payload for the first render of the primary Inbox UI.
/// </summary>
/// <param name="Counts">Unread and domain-action counts. See <see cref="InboxSummaryCounts"/> for the exact semantics.</param>
/// <param name="Feed">Up to 20 newest unified feed items, including both read and unread items. Use <c>GET /api/inbox/feed</c> for pagination.</param>
/// <param name="UnreadFeed">Up to 20 newest feed items whose inbox read timestamp is null. This is not an action-required filter.</param>
/// <param name="PendingTerms">All terms still awaiting customer acceptance, regardless of whether their feed item has been read.</param>
public sealed record InboxOverviewResponse(
    InboxSummaryCounts Counts,
    IReadOnlyList<InboxFeedItemResponse> Feed,
    IReadOnlyList<InboxFeedItemResponse> UnreadFeed,
    IReadOnlyList<PendingTermResponse> PendingTerms);
