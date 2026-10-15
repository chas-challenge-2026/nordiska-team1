namespace Nordiska.Modules.Inbox.Contracts.Responses;

/// <summary>
/// One item in the unified customer inbox feed.
/// </summary>
/// <param name="Id">Required opaque ID used by the unified read endpoint. Exact formats: <c>thread-{sourceId}</c>, <c>notification-{sourceId}</c>, <c>document-{sourceId}</c>, or <c>term-{sourceId}</c>. Example: <c>thread-12</c>.</param>
/// <param name="Type">Required canonical discriminator. Exact values: <c>thread</c>, <c>notification</c>, <c>document</c>, or <c>term</c>.</param>
/// <param name="SourceId">Required positive numeric ID of the underlying thread, notification, document, or term. Use <c>Id</c>, not this field, with the unified read endpoint.</param>
/// <param name="Title">Required display title or thread subject.</param>
/// <param name="Preview">Nullable short preview, filename, or status text. Maximum 1000 characters when present.</param>
/// <param name="Category">Nullable display/filter label. Its vocabulary depends on the item type, for example <c>Allmänt</c>, <c>TaxReport</c>, or <c>Villkor</c>.</param>
/// <param name="Priority">Required case-sensitive display value: <c>Normal</c>, <c>Important</c>, or <c>Critical</c>.</param>
/// <param name="IsRead"><c>true</c> when the feed item has been acknowledged. This is independent of <c>ActionRequired</c>.</param>
/// <param name="ActionRequired"><c>true</c> when a separate domain action remains, for example accepting a term. Reading the item does not perform that action.</param>
/// <param name="OccurredAt">Required ISO-8601 timestamp for the latest message/event or publication time. The feed is ordered descending by this field.</param>
/// <param name="TargetUrl">Nullable backend API resource/action URL. It is not a frontend navigation route. Typical values are <c>/api/inbox/threads/{sourceId}</c>, <c>/api/inbox/documents/{sourceId}/download</c>, or <c>/api/inbox/terms/{sourceId}/accept</c>.</param>
public sealed record InboxFeedItemResponse(
    string Id,
    string Type,
    long SourceId,
    string Title,
    string? Preview,
    string? Category,
    string Priority,
    bool IsRead,
    bool ActionRequired,
    DateTimeOffset OccurredAt,
    string? TargetUrl)
{
    /// <summary>
    /// Backwards-compatible alias of <see cref="OccurredAt"/>. Both JSON fields contain the same timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt => OccurredAt;
}
