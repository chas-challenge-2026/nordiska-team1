namespace Nordiska.Modules.Inbox.Contracts.Responses;

/// <summary>
/// Response model representing a customer document in the digital archive.
/// </summary>
/// <param name="Id">Unique customer-document relationship ID.</param>
/// <param name="DocumentId">Underlying document ID in the hub.</param>
/// <param name="DocumentType">Classification of the document (e.g. 'TaxReport', 'AnnualStatement', 'Agreement', 'Terms').</param>
/// <param name="Title">Descriptive title of the document.</param>
/// <param name="FileName">Original filename of the stored PDF file.</param>
/// <param name="MimeType">MIME type (e.g. 'application/pdf').</param>
/// <param name="FileSizeBytes">Size of the document in bytes.</param>
/// <param name="Sha256">Cryptographic SHA-256 hash for integrity verification.</param>
/// <param name="Status">Document lifecycle status: 'Draft', 'Published', 'Archived', 'Deleted'.</param>
/// <param name="PublishedAt">Timestamp when the document was published to the customer.</param>
/// <param name="FirstOpenedAt">Timestamp when the customer first opened/downloaded the document (for legal compliance).</param>
/// <param name="HasBeenOpened">Whether the document has been opened at least once.</param>
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
)
{
    public DateTimeOffset CreatedAt => PublishedAt;
    public bool IsRead => HasBeenOpened;
    public string? Category => DocumentType;
    public string DownloadUrl => $"/api/inbox/documents/{DocumentId}/download";
}