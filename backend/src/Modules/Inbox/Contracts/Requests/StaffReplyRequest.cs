namespace Nordiska.Modules.Inbox.Contracts.Requests;

/// <summary>
/// Request for bank staff to reply to a customer thread.
/// </summary>
/// <param name="Body">Staff message reply text.</param>
/// <param name="ReplyAllowed">Whether the customer is allowed to reply to this message.</param>
public sealed record StaffReplyRequest(
    string Body,
    bool ReplyAllowed = true);
