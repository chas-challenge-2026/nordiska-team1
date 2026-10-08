namespace Nordiska.Modules.Inbox.Contracts.Requests;

/// <summary>
/// Query parameters for filtering and paginating the unified inbox feed.
/// </summary>
/// <param name="Type">Optional filter by item type ('thread', 'notification', 'document', 'term').</param>
/// <param name="UnreadOnly">If true, returns only unread/unopened/action-required items.</param>
/// <param name="Page">Page number (1-based, default 1).</param>
/// <param name="PageSize">Page size (1-100, default 20).</param>
public sealed record FeedQueryParameters(
    string? Type = null,
    bool UnreadOnly = false,
    int Page = 1,
    int PageSize = 20);