using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Nordiska.Modules.Reporting.Application;
using Nordiska.Modules.Reporting.Domain;
using Nordiska.Modules.Reporting.Infrastructure.Db;

namespace Nordiska.Modules.Reporting.Infrastructure;

public sealed class TaxReportCompletionRepository
    : ITaxReportCompletionRepository
{
    internal const string CompleteSql = """
        WITH owned_job AS MATERIALIZED (
            SELECT job."Id"
            FROM reporting.tax_report_jobs AS job
            WHERE job."Id" = @JobId
              AND job."TaxReportId" = @TaxReportId
              AND job."Status" = @ProcessingStatus
              AND job."LockedBy" = @WorkerId
            FOR UPDATE
        ),
        existing_document AS MATERIALIZED (
            SELECT document."Id", document."Sha256Hash"
            FROM reporting.generated_documents AS document
            WHERE document."StorageKey" = @StorageKey
              AND EXISTS (
                  SELECT 1
                  FROM owned_job
              )
        ),
        inserted_document AS (
            INSERT INTO reporting.generated_documents (
                "DocumentType",
                "ContentType",
                "FileName",
                "StorageKey",
                "ByteLength",
                "Sha256Hash",
                "CreatedAt"
            )
            SELECT
                @DocumentType,
                @ContentType,
                @FileName,
                @StorageKey,
                @ByteLength,
                @Sha256Hash,
                @CompletedAt
            FROM owned_job
            WHERE NOT EXISTS (
                SELECT 1
                FROM existing_document
            )
            ON CONFLICT ("StorageKey") DO NOTHING
            RETURNING "Id", "Sha256Hash"
        ),
        document AS MATERIALIZED (
            SELECT "Id", "Sha256Hash"
            FROM existing_document
            UNION ALL
            SELECT "Id", "Sha256Hash"
            FROM inserted_document
        ),
        accepted_document AS MATERIALIZED (
            SELECT "Id"
            FROM document
            WHERE lower("Sha256Hash") = lower(@Sha256Hash)
        ),
        linked_document AS (
            INSERT INTO reporting.tax_report_documents (
                "TaxReportId",
                "DocumentId",
                "CreatedAt"
            )
            SELECT
                @TaxReportId,
                accepted_document."Id",
                @CompletedAt
            FROM accepted_document
            ON CONFLICT ("TaxReportId", "DocumentId") DO NOTHING
            RETURNING 1
        ),
        completed_job AS (
            UPDATE reporting.tax_report_jobs AS job
            SET
                "Status" = @CompletedStatus,
                "CompletedAt" = @CompletedAt,
                "LockedBy" = NULL,
                "LeaseExpiresAt" = NULL,
                "LastError" = NULL
            FROM owned_job
            WHERE job."Id" = owned_job."Id"
              AND EXISTS (
                  SELECT 1
                  FROM accepted_document
              )
            RETURNING 1
        )
        SELECT
            EXISTS (SELECT 1 FROM owned_job),
            EXISTS (SELECT 1 FROM document),
            EXISTS (SELECT 1 FROM accepted_document),
            EXISTS (SELECT 1 FROM completed_job);
        """;

    private readonly ReportingDbContext _dbContext;

    public TaxReportCompletionRepository(
        ReportingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task CompleteAsync(
        long jobId,
        string workerId,
        CompletedReportDocument completedDocument,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);

        NpgsqlConnection connection =
            (NpgsqlConnection)_dbContext.Database.GetDbConnection();
        bool closeConnection = connection.State != ConnectionState.Open;
        if (closeConnection)
            await connection.OpenAsync(cancellationToken);

        try
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                CompletionResult result =
                    await ExecuteCompletionAsync(
                        connection,
                        jobId,
                        workerId,
                        completedDocument,
                        completedAt,
                        cancellationToken);

                if (!result.JobOwned)
                {
                    throw new InvalidOperationException(
                        "Tax report job is not owned by this worker.");
                }

                if (!result.DocumentFound && attempt == 0)
                    continue;

                if (!result.DocumentFound)
                {
                    throw new InvalidOperationException(
                        "Generated document could not be created or loaded.");
                }

                if (!result.HashAccepted)
                {
                    throw new InvalidDataException(
                        "Existing document metadata has a different hash.");
                }

                if (!result.JobCompleted)
                {
                    throw new InvalidOperationException(
                        "Tax report job could not be completed.");
                }

                return;
            }
        }
        finally
        {
            if (closeConnection)
                await connection.CloseAsync();
        }
    }

    private static async Task<CompletionResult> ExecuteCompletionAsync(
        NpgsqlConnection connection,
        long jobId,
        string workerId,
        CompletedReportDocument completedDocument,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            CompleteSql,
            connection);
        command.Parameters.AddWithValue("JobId", jobId);
        command.Parameters.AddWithValue(
            "TaxReportId",
            completedDocument.TaxReportId);
        command.Parameters.AddWithValue(
            "ProcessingStatus",
            TaxReportJobStatuses.Processing);
        command.Parameters.AddWithValue(
            "CompletedStatus",
            TaxReportJobStatuses.Completed);
        command.Parameters.AddWithValue("WorkerId", workerId);
        command.Parameters.AddWithValue(
            "DocumentType",
            "AnnualTaxReport");
        command.Parameters.AddWithValue(
            "ContentType",
            "application/pdf");
        command.Parameters.AddWithValue(
            "FileName",
            completedDocument.FileName);
        command.Parameters.AddWithValue(
            "StorageKey",
            completedDocument.StorageKey);
        command.Parameters.AddWithValue(
            "ByteLength",
            completedDocument.ByteLength);
        command.Parameters.AddWithValue(
            "Sha256Hash",
            completedDocument.Sha256Hash);
        command.Parameters.AddWithValue(
            "CompletedAt",
            completedAt.ToUniversalTime());

        await using NpgsqlDataReader reader =
            await command.ExecuteReaderAsync(
                cancellationToken);
        await reader.ReadAsync(cancellationToken);

        return new CompletionResult(
            reader.GetBoolean(0),
            reader.GetBoolean(1),
            reader.GetBoolean(2),
            reader.GetBoolean(3));
    }

    private sealed record CompletionResult(
        bool JobOwned,
        bool DocumentFound,
        bool HashAccepted,
        bool JobCompleted);
}
