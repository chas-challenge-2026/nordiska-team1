namespace Nordiska.Modules.Inbox.Contracts.Responses;

/// <summary>
/// General public or bank-wide document (e.g. general terms, policy, price list).
/// </summary>
/// <param name="Id">Unique document identifier.</param>
/// <param name="DocumentType">Classification (e.g. 'GeneralTerms', 'PrivacyPolicy', 'PriceList', 'Agreement').</param>
/// <param name="Title">Descriptive title of the document.</param>
/// <param name="FileName">Original filename of the document.</param>
/// <param name="MimeType">MIME type (e.g. 'application/pdf').</param>
/// <param name="FileSizeBytes">Size of the document in bytes.</param>
/// <param name="Sha256">Cryptographic SHA-256 hash for integrity verification.</param>
/// <param name="PublishedAt">Publication timestamp.</param>
/// <param name="DownloadUrl">API URL to securely download the document.</param>
public sealed record GeneralDocumentResponse(
    long Id,
    string DocumentType,
    string Title,
    string FileName,
    string MimeType,
    long FileSizeBytes,
    string Sha256,
    DateTimeOffset PublishedAt,
    string DownloadUrl)
{
    public DateTimeOffset CreatedAt => PublishedAt;
    public bool IsRead => true;
    public string Category => DocumentType;
}