namespace Nordiska.Modules.Documents.Domain;
public sealed class Document
{
    public long Id { get; private set; }
    public string DocumentType { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public string MimeType { get; private set; } = string.Empty;
    public string StorageKey { get; private set; } = string.Empty;
    public long FileSizeBytes { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;
    public DocumentStatus Status { get; private set; }
    public string SourceType { get; private set; } = string.Empty;
    public string? SourceId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    private Document() { }
    public Document(string documentType,string title,string fileName,string mimeType,string storageKey,long fileSizeBytes,string sha256,string sourceType,string? sourceId=null)
    {DocumentType=documentType; Title=title; FileName=fileName; MimeType=mimeType; StorageKey=storageKey; FileSizeBytes=fileSizeBytes; Sha256=sha256; SourceType=sourceType; SourceId=sourceId; Status=DocumentStatus.Available; CreatedAt=DateTimeOffset.UtcNow;}
    public void Archive()=>Status=DocumentStatus.Archived;
}
public enum DocumentStatus { Available=1, Archived=2, Deleted=3 }
