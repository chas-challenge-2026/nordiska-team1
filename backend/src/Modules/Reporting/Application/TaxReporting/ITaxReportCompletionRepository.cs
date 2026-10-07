namespace Nordiska.Modules.Reporting.Application;

public sealed record CompletedReportDocument(
    long TaxReportId,
    string FileName,
    string StorageKey,
    long ByteLength,
    string Sha256Hash);

public interface ITaxReportCompletionRepository
{
    Task CompleteAsync(
        long jobId,
        string workerId,
        CompletedReportDocument document,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken);
}