namespace Nordiska.Modules.Reporting.Application;

public sealed record CompletedAccountStatementDocument(
    long AccountStatementId,
    string FileName,
    string StorageKey,
    long ByteLength,
    string Sha256Hash);

public interface IAccountStatementCompletionRepository
{
    Task CompleteAsync(
        long jobId,
        string workerId,
        CompletedAccountStatementDocument document,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken);
}
