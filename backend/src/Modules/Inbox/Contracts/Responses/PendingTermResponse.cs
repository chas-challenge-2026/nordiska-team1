namespace Nordiska.Modules.Inbox.Contracts.Responses;

/// <summary>
/// Representation of a term/condition awaiting customer acceptance.
/// </summary>
public sealed record PendingTermResponse(
    long AcceptanceId,
    long TermId,
    string Code,
    int Version,
    string Title,
    long DocumentId,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset PublishedAt,
    string Status,
    DateTimeOffset? CreatedAt,
    string DownloadUrl
);