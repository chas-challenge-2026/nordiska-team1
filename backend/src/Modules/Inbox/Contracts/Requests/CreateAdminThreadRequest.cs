namespace Nordiska.Modules.Inbox.Contracts.Requests;

/// <summary>
/// Request for creating a new support conversation or information broadcast from admin.
/// </summary>
/// <param name="CustomerId">Target customer ID (required if BroadcastToAll is false).</param>
/// <param name="BroadcastToAll">Whether to send message to all registered customers.</param>
/// <param name="Subject">Subject of the thread.</param>
/// <param name="Body">Content of the message.</param>
/// <param name="Category">Category label (e.g. 'Information', 'Ränta', 'Allmänt').</param>
/// <param name="ReplyAllowed">Whether customer is permitted to reply.</param>
/// <param name="IsInformationOnly">Whether to present as announcement / info banner in UI.</param>
public sealed record CreateAdminThreadRequest(
    long? CustomerId,
    bool BroadcastToAll,
    string Subject,
    string Body,
    string? Category = null,
    bool ReplyAllowed = true,
    bool IsInformationOnly = false);