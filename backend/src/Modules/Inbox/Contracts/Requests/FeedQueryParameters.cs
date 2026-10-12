namespace Nordiska.Modules.Inbox.Contracts.Requests;

/// <summary>
/// Query parameters for filtering and paginating the unified inbox feed.
/// </summary>
/// <param name="Type">Optional type filter. Canonical values are <c>thread</c>, <c>notification</c>, <c>document</c>, and <c>term</c>. Accepted aliases are <c>threads</c>, <c>message</c>, <c>notifications</c>, <c>documents</c>, and <c>terms</c>. Null includes every supported type.</param>
/// <param name="UnreadOnly"><c>true</c> includes only feed items whose <c>ReadAt</c> value is null. It does not filter by <c>ActionRequired</c>.</param>
/// <param name="Page">One-based page number. Minimum 1. Default 1.</param>
/// <param name="PageSize">Number of items per page. Minimum 1, maximum 100. Default 20.</param>
public sealed record FeedQueryParameters(
    string? Type = null,
    bool UnreadOnly = false,
    int Page = 1,
    int PageSize = 20);
