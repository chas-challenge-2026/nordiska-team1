namespace Nordiska.Modules.Inbox.Contracts.Responses;

/// <summary>
/// Representation of a single message within a thread.
/// </summary>
/// <param name="Id">Message ID.</param>
/// <param name="ThreadId">Thread ID.</param>
/// <param name="SenderType">Sender type ('Customer', 'Bank', 'System').</param>
/// <param name="SenderName">Display name of sender (e.g. 'Nordiska Sparbanken' or 'Kund').</param>
/// <param name="SenderCustomerId">Sender Customer ID if sent by customer, or staff ID if sent by bank/staff.</param>
/// <param name="Body">Content of the message.</param>
/// <param name="ReplyAllowed">Whether reply is allowed on this message.</param>
/// <param name="SentAt">Timestamp when message was sent.</param>
public sealed record MessageResponse(
    long Id,
    long ThreadId,
    string SenderType,
    string SenderName,
    long? SenderCustomerId,
    string Body,
    bool ReplyAllowed,
    DateTimeOffset SentAt);
