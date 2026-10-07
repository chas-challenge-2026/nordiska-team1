namespace Nordiska.Modules.Reporting.Application;

public sealed record ClaimedAccountStatementJob(
    long JobId,
    long AccountStatementId,
    int AttemptNumber);

public interface IAccountStatementJobRepository
{
    Task<ClaimedAccountStatementJob?> ClaimNextAsync(
        string workerId,
        DateTimeOffset now,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken);

    Task MarkFailedAsync(
        long jobId,
        string workerId,
        string error,
        DateTimeOffset failedAt,
        TimeSpan retryDelay,
        int maximumAttempts,
        CancellationToken cancellationToken);
}
