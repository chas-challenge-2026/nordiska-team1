namespace Nordiska.Modules.Inbox.Contracts.Responses;

/// <summary>
/// Result of marking every unread unified feed item as read.
/// </summary>
/// <param name="Success"><c>true</c> when the operation completed, including when no unread items existed.</param>
/// <param name="ThreadsMarkedAsRead">Thread feed items changed from unread to read. Minimum 0.</param>
/// <param name="NotificationsMarkedAsRead">Standalone notification feed items changed from unread to read. Minimum 0.</param>
/// <param name="TotalMarkedAsRead">Sum of the four per-type counts. Minimum 0.</param>
/// <param name="Message">Human-readable confirmation for display or logging. Frontend logic should use the numeric fields.</param>
/// <param name="DocumentsMarkedAsRead">Document feed items changed from unread to read. This does not open/download the document. Minimum 0.</param>
/// <param name="TermsMarkedAsRead">Term feed items changed from unread to read. This does not accept the term. Minimum 0.</param>
public sealed record MarkAllReadResponse(
    bool Success,
    int ThreadsMarkedAsRead,
    int NotificationsMarkedAsRead,
    int TotalMarkedAsRead,
    string Message,
    int DocumentsMarkedAsRead = 0,
    int TermsMarkedAsRead = 0);
