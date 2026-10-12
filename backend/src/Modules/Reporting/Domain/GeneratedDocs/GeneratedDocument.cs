namespace Nordiska.Modules.Reporting.Domain;

public sealed class GeneratedDocument
{
    private GeneratedDocument()
    {
    }

    private GeneratedDocument(
        string documentType,
        string contentType,
        string fileName,
        string storageKey,
        long byteLength,
        string sha256Hash,
        DateTimeOffset createdAt)
    {
        DocumentType = documentType;
        ContentType = contentType;
        FileName = fileName;
        StorageKey = storageKey;
        ByteLength = byteLength;
        Sha256Hash = sha256Hash;
        CreatedAt = createdAt;
    }

    public long Id { get; private set; }

    public string DocumentType { get; private set; } = null!;

    public string ContentType { get; private set; } = null!;

    public string FileName { get; private set; } = null!;

    public string StorageKey { get; private set; } = null!;

    public long ByteLength { get; private set; }

    public string Sha256Hash { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    public static GeneratedDocument Create(
        string documentType,
        string contentType,
        string fileName,
        string storageKey,
        long byteLength,
        string sha256Hash,
        DateTimeOffset createdAt)
    {
        return new GeneratedDocument(
            documentType,
            contentType,
            fileName,
            storageKey,
            byteLength,
            sha256Hash,
            createdAt);
    }
}