namespace Nordiska.Modules.Inbox.Contracts.Responses;

/// <summary>
/// Result of a document download operation including binary content and verification metadata.
/// </summary>
public sealed record DocumentDownloadResult(
    string FileName,
    string MimeType,
    byte[] Content,
    string Sha256,
    bool IsChecksumValid
);