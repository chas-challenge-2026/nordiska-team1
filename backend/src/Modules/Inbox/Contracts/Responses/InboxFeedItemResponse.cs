namespace Nordiska.Modules.Inbox.Contracts.Responses;

/// <summary>
/// Aggregated timeline item representing any event in the unified customer inbox feed.
/// </summary>
/// <param name="Id">Unique feed item identifier (e.g. 'thread-12', 'notification-4', 'document-7', 'term-2').</param>
/// <param name="Type">Item classification: 'thread', 'notification', 'document', or 'term'.</param>
/// <param name="SourceId">Database ID of the underlying resource.</param>
/// <param name="Title">Subject or title of the item.</param>
/// <param name="Preview">Short preview or summary of the content.</param>
/// <param name="Category">Category or document type label (e.g. 'Sparkonto', 'TaxReport', 'Allmänt').</param>
/// <param name="Priority">Item priority: 'Normal', 'Important', or 'Critical'.</param>
/// <param name="IsRead">Whether the item has been read or opened by the customer.</param>
/// <param name="ActionRequired">Whether this item requires urgent customer action (e.g. unaccepted terms).</param>
/// <param name="OccurredAt">Timestamp of the most recent event or publication date.</param>
/// <param name="TargetUrl">Convenience API endpoint or navigation path for the resource.</param>
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
    public DateTimeOffset CreatedAt => OccurredAt;
}