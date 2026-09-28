using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Nordiska.Modules.Reporting.Application;
using Nordiska.Modules.Reporting.Domain;
using Nordiska.Modules.Reporting.Infrastructure.Db;

namespace Nordiska.Modules.Reporting.Infrastructure;

public sealed class TaxReportJobRepository(ReportingDbContext db)
    : ITaxReportJobRepository
{
    public async Task CreateAsync(
        TaxReportJob job,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        db.TaxReportJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<TaxReportJob?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return db.TaxReportJobs
            .SingleOrDefaultAsync(job => job.Id == id, cancellationToken);
    }

    public Task<TaxReportJob?> GetActiveAsync(
        long customerId,
        long accountId,
        int year,
        CancellationToken cancellationToken = default)
    {
        return db.TaxReportJobs
            .AsNoTracking()
            .Where(job => job.CustomerId == customerId
                && job.AccountId == accountId
                && job.Year == year
                && (job.Status == "Pending" || job.Status == "Processing"))
            .OrderByDescending(job => job.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<TaxReportJob?> ClaimNextPendingAsync(
        CancellationToken cancellationToken = default)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State == ConnectionState.Closed;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                WITH next_job AS (
                    SELECT "Id"
                    FROM reporting.tax_report_jobs
                    WHERE "Status" = 'Pending'
                    ORDER BY "CreatedAt", "Id"
                    FOR UPDATE SKIP LOCKED
                    LIMIT 1
                ), claimed AS (
                    UPDATE reporting.tax_report_jobs AS job
                    SET "Status" = 'Processing',
                        "StartedAt" = NOW(),
                        "UpdatedAt" = NOW()
                    FROM next_job
                    WHERE job."Id" = next_job."Id"
                    RETURNING job.*
                )
                SELECT * FROM claimed
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return !await reader.ReadAsync(cancellationToken)
                ? null
                : ReadJob(reader);
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<int> RequeueStaleProcessingAsync(
        TimeSpan staleAfter,
        CancellationToken cancellationToken = default)
    {
        if (staleAfter <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(staleAfter));
        }

        var cutoff = DateTime.UtcNow - staleAfter;

        return await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE reporting.tax_report_jobs
            SET "Status" = 'Pending',
                "StartedAt" = NULL,
                "UpdatedAt" = NOW()
            WHERE "Status" = 'Processing'
              AND "StartedAt" < {cutoff}
            """, cancellationToken);
    }

    public async Task UpdateAsync(
        TaxReportJob job,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        db.TaxReportJobs.Update(job);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static TaxReportJob ReadJob(DbDataReader reader)
    {
        return new TaxReportJob
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            CustomerId = reader.GetInt64(reader.GetOrdinal("CustomerId")),
            AccountId = reader.GetInt64(reader.GetOrdinal("AccountId")),
            Year = reader.GetInt32(reader.GetOrdinal("Year")),
            Status = reader.GetString(reader.GetOrdinal("Status")),
            DownloadUrl = ReadNullableString(reader, "DownloadUrl"),
            ErrorCode = ReadNullableInt(reader, "ErrorCode"),
            ErrorMessage = ReadNullableString(reader, "ErrorMessage"),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            StartedAt = ReadNullableDateTime(reader, "StartedAt"),
            CompletedAt = ReadNullableDateTime(reader, "CompletedAt"),
            UpdatedAt = reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
        };
    }

    private static string? ReadNullableString(DbDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static int? ReadNullableInt(DbDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private static DateTime? ReadNullableDateTime(DbDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }
}
