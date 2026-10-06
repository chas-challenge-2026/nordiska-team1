namespace Nordiska.Modules.Inbox.Contracts.Responses;

/// <summary>
/// General term or policy version details.
/// </summary>
public sealed record TermResponse(
    long Id,
    string Code,
    int Version,
    string Title,
    long DocumentId,
    string Status,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset PublishedAt,
    string? DownloadUrl
);