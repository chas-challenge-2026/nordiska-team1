namespace Nordiska.Modules.Inbox.Contracts.Responses;

/// <summary>
/// Customer notification response model.
/// </summary>
/// <param name="Id">Notification ID.</param>
/// <param name="CustomerId">Customer ID.</param>
/// <param name="Type">Notification category type.</param>
/// <param name="Priority">Priority ('Low', 'Normal', 'High', 'Critical').</param>
/// <param name="Title">Notification header/title.</param>
/// <param name="Body">Detailed message body.</param>
/// <param name="TargetType">Target entity type ('Document', 'MessageThread', 'Term', 'Loan').</param>
/// <param name="TargetId">ID of target resource.</param>
/// <param name="IsRead">Whether notification has been read.</param>
/// <param name="CreatedAt">Created timestamp.</param>
/// <param name="ReadAt">Read timestamp if read.</param>
public sealed record CustomerNotificationResponse(
    long Id,
    long CustomerId,
    string Type,
    string Priority,
    string Title,
    string? Body,
    string? TargetType,
    long? TargetId,
    bool IsRead,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);