namespace Nordiska.Modules.Inbox.Contracts.Responses;

/// <summary>
/// Response model representing a customer document in the digital archive.
/// </summary>
public sealed record DocumentResponse(
    long Id,
    long DocumentId,
    string DocumentType,
    string Title,
    string FileName,
    string MimeType,
    long FileSizeBytes,
    string Sha256,
    string Status,
    DateTimeOffset PublishedAt,
    DateTimeOffset? FirstOpenedAt,
    bool HasBeenOpened
);