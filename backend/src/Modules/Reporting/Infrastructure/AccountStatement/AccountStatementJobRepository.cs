using System.Data;
using Microsoft.EntityFrameworkCore;
using Nordiska.Modules.Reporting.Application;
using Nordiska.Modules.Reporting.Domain;
using Nordiska.Modules.Reporting.Infrastructure.Db;

namespace Nordiska.Modules.Reporting.Infrastructure;

public sealed class AccountStatementJobRepository
    : IAccountStatementJobRepository
{
    private readonly ReportingDbContext _dbContext;

    public AccountStatementJobRepository(
        ReportingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ClaimedAccountStatementJob?> ClaimNextAsync(
        string workerId,
        DateTimeOffset now,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

        List<AccountStatementJob> jobs =
            await _dbContext.AccountStatementJobs
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM reporting.account_statement_jobs
                    WHERE
                        (
                            "Status" = {AccountStatementJobStatuses.Pending}
                            AND "AvailableAt" <= {now}
                        )
                        OR
                        (
                            "Status" = {AccountStatementJobStatuses.Processing}
                            AND "LeaseExpiresAt" <= {now}
                        )
                    ORDER BY "CreatedAt", "Id"
                    FOR UPDATE SKIP LOCKED
                    LIMIT 1
                    """)
                .ToListAsync(cancellationToken);

        AccountStatementJob? job = jobs.SingleOrDefault();

        if (job is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        job.Claim(workerId, now, leaseDuration);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ClaimedAccountStatementJob(
            job.Id,
            job.AccountStatementId,
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
        AccountStatementJob job =
            await _dbContext.AccountStatementJobs.SingleAsync(
                candidate =>
                    candidate.Id == jobId &&
                    candidate.Status == AccountStatementJobStatuses.Processing &&
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
