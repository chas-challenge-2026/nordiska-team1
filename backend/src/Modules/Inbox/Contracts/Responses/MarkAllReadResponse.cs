namespace Nordiska.Modules.Inbox.Contracts.Responses;

/// <summary>
/// Result of marking all threads and notifications as read.
/// </summary>
/// <param name="Success">Indicates whether the operation succeeded.</param>
/// <param name="ThreadsMarkedAsRead">Number of threads marked as read.</param>
/// <param name="NotificationsMarkedAsRead">Number of notifications marked as read.</param>
/// <param name="TotalMarkedAsRead">Total count of items marked as read.</param>
/// <param name="Message">Human-readable confirmation message.</param>
public sealed record MarkAllReadResponse(
    bool Success,
    int ThreadsMarkedAsRead,
    int NotificationsMarkedAsRead,
    int TotalMarkedAsRead,
    string Message);