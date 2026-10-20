namespace Nordiska.Modules.Reporting.Application;

public sealed record AccountStatementJobStatus(
    long JobId,
    long AccountStatementId,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    string? Error);

public sealed record AccountStatementDownload(
    string FileName,
    string ContentType,
    byte[] Content);

public interface IAccountStatementQueryService
{
    Task<AccountStatementJobStatus?> GetStatusAsync(
        long customerId,
        long jobId,
        CancellationToken cancellationToken);

    Task<AccountStatementDownload?> DownloadAsync(
        long customerId,
        long accountStatementId,
        CancellationToken cancellationToken);
}
