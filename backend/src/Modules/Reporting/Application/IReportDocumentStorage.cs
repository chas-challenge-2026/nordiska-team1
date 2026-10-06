namespace Nordiska.Modules.Reporting.Application;

public interface IReportDocumentStorage
{
    Task SaveAsync(
        string storageKey,
        ReadOnlyMemory<byte> content,
        string expectedSha256Hash,
        CancellationToken cancellationToken);

    Task<byte[]> ReadAsync(
        string storageKey,
        string expectedSha256Hash,
        CancellationToken cancellationToken);
}