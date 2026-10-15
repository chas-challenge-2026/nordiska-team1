namespace Nordiska.Modules.Inbox.Contracts.Requests;

/// <summary>
/// Query parameters for fetching customer notifications.
/// </summary>
/// <param name="UnreadOnly">Filter only unread notifications.</param>
/// <param name="Page">Page number (1-based, default 1).</param>
/// <param name="PageSize">Page size (1-100, default 20).</param>
public sealed record NotificationQueryParameters(
    bool UnreadOnly = false,
    int Page = 1,
    int PageSize = 20);