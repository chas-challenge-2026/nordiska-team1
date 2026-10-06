using System.Data;
using Microsoft.EntityFrameworkCore;
using Nordiska.Modules.Reporting.Application;
using Nordiska.Modules.Reporting.Domain;
using Nordiska.Modules.Reporting.Infrastructure.Db;

namespace Nordiska.Modules.Reporting.Infrastructure;

public sealed class TaxReportJobRepository
    : ITaxReportJobRepository
{
    private readonly ReportingDbContext _dbContext;

    public TaxReportJobRepository(
        ReportingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ClaimedTaxReportJob?> ClaimNextAsync(
        string workerId,
        DateTimeOffset now,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

        List<TaxReportJob> jobs =
            await _dbContext.TaxReportJobs
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM reporting.tax_report_jobs
                    WHERE
                        (
                            "Status" = {TaxReportJobStatuses.Pending}
                            AND "AvailableAt" <= {now}
                        )
                        OR
                        (
                            "Status" = {TaxReportJobStatuses.Processing}
                            AND "LeaseExpiresAt" <= {now}
                        )
                    ORDER BY "CreatedAt", "Id"
                    FOR UPDATE SKIP LOCKED
                    LIMIT 1
                    """)
                .ToListAsync(cancellationToken);

        TaxReportJob? job = jobs.SingleOrDefault();

        if (job is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        job.Claim(
            workerId,
            now,
            leaseDuration);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ClaimedTaxReportJob(
            job.Id,
            job.TaxReportId,
            job.AttemptCount);
    }

    public async Task MarkFailedAsync(
        long jobId,
        string workerId,
        string error,
        DateTimeOffset failedAt,
        TimeSpan retryDelay,
        int maximumAttempts,
        CancellationToken cancellationToken)
    {
        TaxReportJob job =
            await _dbContext.TaxReportJobs.SingleAsync(
                candidate =>
                    candidate.Id == jobId &&
                    candidate.Status ==
                        TaxReportJobStatuses.Processing &&
                    candidate.LockedBy == workerId,
                cancellationToken);

        job.MarkFailed(
            workerId,
            error,
            failedAt,
            retryDelay,
            maximumAttempts);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}