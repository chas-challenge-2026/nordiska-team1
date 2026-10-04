using Nordiska.Modules.Communication.Domain;

namespace Nordiska.Modules.Inbox.Application;

public sealed record InboxThreadQuery(
    int Page = 1,
    int PageSize = 20,
    MessageFolder Folder = MessageFolder.Inbox);

public sealed record CreateInboxThreadRequest(
    string Subject,
    string Body);

public sealed record InboxMessageResponse(
    long Id,
    MessageSenderType SenderType,
    string Body,
    bool ReplyAllowed,
    DateTimeOffset SentAt,
    DateTimeOffset? RevokedAt);

public sealed record InboxThreadResponse(
    long Id,
    string Subject,
    MessageThreadStatus Status,
    MessageFolder Folder,
    bool IsRead,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastMessageAt,
    IReadOnlyCollection<InboxMessageResponse> Messages);