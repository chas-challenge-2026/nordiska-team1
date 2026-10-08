using Nordiska.Modules.Reporting.Domain;

namespace Nordiska.Modules.Reporting.Application;

public sealed record ClaimedTaxReportJob(
    long JobId,
    long TaxReportId,
    int AttemptNumber);

public interface ITaxReportJobRepository
{
    Task<ClaimedTaxReportJob?> ClaimNextAsync(
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