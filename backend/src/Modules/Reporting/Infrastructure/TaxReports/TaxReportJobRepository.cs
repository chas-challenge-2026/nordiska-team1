using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Nordiska.Modules.Reporting.Application;
using Nordiska.Modules.Reporting.Domain;
using Nordiska.Modules.Reporting.Infrastructure.Db;

namespace Nordiska.Modules.Reporting.Infrastructure;

public sealed class TaxReportJobRepository
    : ITaxReportJobRepository
{
    internal const string ClaimNextSql = """
        WITH candidate AS (
            SELECT job."Id"
            FROM reporting.tax_report_jobs AS job
            WHERE
                (
                    job."Status" = @PendingStatus
                    AND job."AvailableAt" <= @Now
                )
                OR
                (
                    job."Status" = @ProcessingStatus
                    AND job."LeaseExpiresAt" <= @Now
                )
            ORDER BY job."CreatedAt", job."Id"
            FOR UPDATE SKIP LOCKED
            LIMIT 1
        )
        UPDATE reporting.tax_report_jobs AS job
        SET
            "Status" = @ProcessingStatus,
            "AttemptCount" = job."AttemptCount" + 1,
            "LockedBy" = @WorkerId,
            "LeaseExpiresAt" = @LeaseExpiresAt,
            "StartedAt" = COALESCE(job."StartedAt", @Now),
            "CompletedAt" = NULL,
            "LastError" = NULL
        FROM candidate
        WHERE job."Id" = candidate."Id"
        RETURNING job."Id", job."TaxReportId", job."AttemptCount";
        """;

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
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(leaseDuration));
        }

        NpgsqlConnection connection =
            (NpgsqlConnection)_dbContext.Database.GetDbConnection();
        bool closeConnection = connection.State != ConnectionState.Open;
        if (closeConnection)
            await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = new NpgsqlCommand(
                ClaimNextSql,
                connection);
            command.Parameters.AddWithValue(
                "PendingStatus",
                TaxReportJobStatuses.Pending);
            command.Parameters.AddWithValue(
                "ProcessingStatus",
                TaxReportJobStatuses.Processing);
            command.Parameters.AddWithValue(
                "WorkerId",
                workerId);
            command.Parameters.AddWithValue(
                "Now",
                now.ToUniversalTime());
            command.Parameters.AddWithValue(
                "LeaseExpiresAt",
                now.Add(leaseDuration).ToUniversalTime());

            await using NpgsqlDataReader reader =
                await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
                return null;

            return new ClaimedTaxReportJob(
                reader.GetInt64(0),
                reader.GetInt64(1),
                reader.GetInt32(2));
        }
        finally
        {
            if (closeConnection)
                await connection.CloseAsync();
        }
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
