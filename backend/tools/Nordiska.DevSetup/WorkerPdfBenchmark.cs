using System.Diagnostics;
using System.Globalization;
using Npgsql;

namespace Nordiska.DevSetup;

internal sealed record WorkerPdfBenchmarkResult(
    int Requested,
    int Pending,
    int Processing,
    int Completed,
    int Failed,
    TimeSpan Elapsed,
    double PdfsPerSecond,
    double P50Milliseconds,
    double P95Milliseconds,
    double P99Milliseconds,
    int NewDistinctDocuments,
    bool RequiredDistinctDocuments,
    bool Passed);

internal static class WorkerPdfBenchmark
{
    private const string PendingStatus = "Pending";
    private const string ProcessingStatus = "Processing";
    private const string CompletedStatus = "Completed";
    private const string FailedStatus = "Failed";

    public static async Task<WorkerPdfBenchmarkResult> RunAsync(
        string root,
        int jobCount,
        int timeoutSeconds,
        long taxReportId,
        bool requireDistinctDocuments = false,
        CancellationToken cancellationToken = default)
    {
        if (jobCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(jobCount),
                "Job count must be greater than zero.");
        }

        if (timeoutSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeoutSeconds),
                "Timeout must be greater than zero.");
        }

        string apiPassword = ReadApiPassword(root);
        string connectionString = DatabaseSetup.CreateConnectionString(
            "nordiska_api",
            apiPassword);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        taxReportId = await ResolveTaxReportIdAsync(
            connection,
            taxReportId,
            cancellationToken);

        int documentsBefore = requireDistinctDocuments
            ? await ReadDocumentCountAsync(
                connection,
                taxReportId,
                cancellationToken)
            : 0;

        ConsoleUi.WriteLine();
        ConsoleUi.WriteLine(requireDistinctDocuments
            ? "Reporting Worker timestamp-signed PDF benchmark"
            : "Reporting Worker PDF benchmark");
        ConsoleUi.WriteLine($"Tax report snapshot: {taxReportId}");
        ConsoleUi.WriteLine($"Jobs: {jobCount:N0}");
        ConsoleUi.WriteLine($"Target: all jobs completed in at most {timeoutSeconds:N0} seconds");
        ConsoleUi.WriteLine(
            "The benchmark measures the worker configuration that is currently running, including its current signing/timestamp settings.");

        long[] jobIds = await StageJobsAsync(
            connection,
            taxReportId,
            jobCount,
            cancellationToken);

        ConsoleUi.WriteLine($"Staged {jobCount:N0} jobs. Releasing the batch...");

        var stopwatch = Stopwatch.StartNew();
        await ReleaseJobsAsync(connection, jobIds, cancellationToken);

        JobCounts counts = await ReadCountsAsync(
            connection,
            jobIds,
            cancellationToken);
        TimeSpan lastProgressOutput = TimeSpan.MinValue;

        while (counts.Completed + counts.Failed < jobCount &&
               stopwatch.Elapsed < TimeSpan.FromSeconds(timeoutSeconds))
        {
            if (lastProgressOutput == TimeSpan.MinValue ||
                stopwatch.Elapsed - lastProgressOutput >= TimeSpan.FromSeconds(5))
            {
                ConsoleUi.WriteLine(
                    $"Progress {counts.Completed + counts.Failed:N0}/{jobCount:N0} terminal " +
                    $"(completed {counts.Completed:N0}, failed {counts.Failed:N0}, " +
                    $"processing {counts.Processing:N0}, pending {counts.Pending:N0}) " +
                    $"after {stopwatch.Elapsed.TotalSeconds:F1}s.");
                lastProgressOutput = stopwatch.Elapsed;
            }

            TimeSpan remaining =
                TimeSpan.FromSeconds(timeoutSeconds) - stopwatch.Elapsed;
            TimeSpan delay = remaining < TimeSpan.FromMilliseconds(100)
                ? remaining
                : TimeSpan.FromMilliseconds(100);

            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, cancellationToken);

            counts = await ReadCountsAsync(
                connection,
                jobIds,
                cancellationToken);
        }

        stopwatch.Stop();

        JobLatencyPercentiles percentiles =
            await ReadLatencyPercentilesAsync(
                connection,
                jobIds,
                cancellationToken);

        int newDistinctDocuments = requireDistinctDocuments
            ? await ReadDocumentCountAsync(
                  connection,
                  taxReportId,
                  cancellationToken) - documentsBefore
            : 0;

        double pdfsPerSecond = stopwatch.Elapsed.TotalSeconds > 0
            ? counts.Completed / stopwatch.Elapsed.TotalSeconds
            : 0;
        bool passed =
            counts.Completed == jobCount &&
            counts.Failed == 0 &&
            stopwatch.Elapsed <= TimeSpan.FromSeconds(timeoutSeconds) &&
            (!requireDistinctDocuments ||
             newDistinctDocuments >= jobCount);

        var result = new WorkerPdfBenchmarkResult(
            jobCount,
            counts.Pending,
            counts.Processing,
            counts.Completed,
            counts.Failed,
            stopwatch.Elapsed,
            pdfsPerSecond,
            percentiles.P50Milliseconds,
            percentiles.P95Milliseconds,
            percentiles.P99Milliseconds,
            newDistinctDocuments,
            requireDistinctDocuments,
            passed);

        WriteResult(result, timeoutSeconds);

        if (!passed && counts.Pending > 0)
        {
            int removed = await RemovePendingJobsAsync(
                connection,
                jobIds,
                cancellationToken);
            ConsoleUi.WriteLine(
                $"[OK] Removed {removed:N0} benchmark jobs that were still pending after the measurement.");
        }

        return result;
    }

    private static string ReadApiPassword(string root)
    {
        string[] candidates =
        [
            Path.Combine(root, "infra", ".env"),
            Path.Combine(root, "infra", "v2", ".env")
        ];

        string? envPath = candidates.FirstOrDefault(File.Exists);
        if (envPath is null)
        {
            throw new FileNotFoundException(
                "No local environment file was found. Expected infra/.env or infra/v2/.env.");
        }

        const string key = "NORDISKA_API_PASSWORD";
        foreach (string line in File.ReadLines(envPath))
        {
            string text = line.Trim();
            if (text.Length == 0 || text.StartsWith('#'))
                continue;

            int separator = text.IndexOf('=');
            if (separator <= 0 ||
                !string.Equals(
                    text[..separator].Trim(),
                    key,
                    StringComparison.Ordinal))
            {
                continue;
            }

            string value = text[(separator + 1)..].Trim();
            if (value.Length >= 2 &&
                ((value[0] == '"' && value[^1] == '"') ||
                 (value[0] == '\'' && value[^1] == '\'')))
            {
                value = value[1..^1];
            }

            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        throw new InvalidOperationException(
            $"{key} is missing or empty in {envPath}.");
    }

    private static async Task<long> ResolveTaxReportIdAsync(
        NpgsqlConnection connection,
        long requestedTaxReportId,
        CancellationToken cancellationToken)
    {
        const string latestReportSql = """
            SELECT "Id"
            FROM reporting.tax_reports
            ORDER BY "CreatedAt" DESC, "Id" DESC
            LIMIT 1;
            """;

        const string requestedReportSql = """
            SELECT "Id"
            FROM reporting.tax_reports
            WHERE "Id" = @TaxReportId;
            """;

        await using var command = new NpgsqlCommand(
            requestedTaxReportId == 0
                ? latestReportSql
                : requestedReportSql,
            connection);

        if (requestedTaxReportId != 0)
            command.Parameters.AddWithValue("TaxReportId", requestedTaxReportId);

        object? value = await command.ExecuteScalarAsync(cancellationToken);
        if (value is null || value is DBNull)
        {
            string detail = requestedTaxReportId == 0
                ? "No tax report snapshot exists. Request one through the API before running the benchmark."
                : $"Tax report snapshot {requestedTaxReportId} does not exist.";
            throw new InvalidOperationException(detail);
        }

        return Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    private static async Task<long[]> StageJobsAsync(
        NpgsqlConnection connection,
        long taxReportId,
        int jobCount,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO reporting.tax_report_jobs (
                "TaxReportId",
                "Status",
                "AttemptCount",
                "CreatedAt",
                "AvailableAt"
            )
            SELECT
                @TaxReportId,
                @PendingStatus,
                0,
                @CreatedAt,
                @HeldUntil
            FROM generate_series(1, @JobCount)
            RETURNING "Id";
            """;

        DateTimeOffset createdAt = DateTimeOffset.UtcNow;

        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            sql,
            connection,
            transaction);
        command.Parameters.AddWithValue("TaxReportId", taxReportId);
        command.Parameters.AddWithValue("PendingStatus", PendingStatus);
        command.Parameters.AddWithValue("CreatedAt", createdAt);
        command.Parameters.AddWithValue("HeldUntil", createdAt.AddDays(1));
        command.Parameters.AddWithValue("JobCount", jobCount);

        var jobIds = new long[jobCount];
        var index = 0;
        await using (NpgsqlDataReader reader =
                     await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
                jobIds[index++] = reader.GetInt64(0);
        }

        if (index != jobCount)
        {
            throw new InvalidOperationException(
                $"Expected {jobCount:N0} staged jobs but PostgreSQL returned {index:N0} IDs.");
        }

        await transaction.CommitAsync(cancellationToken);
        return jobIds;
    }

    private static async Task ReleaseJobsAsync(
        NpgsqlConnection connection,
        long[] jobIds,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE reporting.tax_report_jobs AS job
            SET "AvailableAt" = @AvailableAt
            WHERE job."Id" = ANY(@JobIds);
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("AvailableAt", DateTimeOffset.UtcNow);
        command.Parameters.AddWithValue("JobIds", jobIds);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<JobCounts> ReadCountsAsync(
        NpgsqlConnection connection,
        long[] jobIds,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                count(*) FILTER (WHERE job."Status" = @PendingStatus),
                count(*) FILTER (WHERE job."Status" = @ProcessingStatus),
                count(*) FILTER (WHERE job."Status" = @CompletedStatus),
                count(*) FILTER (WHERE job."Status" = @FailedStatus)
            FROM reporting.tax_report_jobs AS job
            WHERE job."Id" = ANY(@JobIds);
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("PendingStatus", PendingStatus);
        command.Parameters.AddWithValue("ProcessingStatus", ProcessingStatus);
        command.Parameters.AddWithValue("CompletedStatus", CompletedStatus);
        command.Parameters.AddWithValue("FailedStatus", FailedStatus);
        command.Parameters.AddWithValue("JobIds", jobIds);

        await using NpgsqlDataReader reader =
            await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);

        return new JobCounts(
            checked((int)reader.GetInt64(0)),
            checked((int)reader.GetInt64(1)),
            checked((int)reader.GetInt64(2)),
            checked((int)reader.GetInt64(3)));
    }

    private static async Task<JobLatencyPercentiles> ReadLatencyPercentilesAsync(
        NpgsqlConnection connection,
        long[] jobIds,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                COALESCE(
                    percentile_cont(0.50) WITHIN GROUP (
                        ORDER BY EXTRACT(EPOCH FROM (job."CompletedAt" - job."StartedAt")) * 1000
                    ) FILTER (
                        WHERE job."Status" = @CompletedStatus
                          AND job."StartedAt" IS NOT NULL
                          AND job."CompletedAt" IS NOT NULL
                    ),
                    0
                )::double precision,
                COALESCE(
                    percentile_cont(0.95) WITHIN GROUP (
                        ORDER BY EXTRACT(EPOCH FROM (job."CompletedAt" - job."StartedAt")) * 1000
                    ) FILTER (
                        WHERE job."Status" = @CompletedStatus
                          AND job."StartedAt" IS NOT NULL
                          AND job."CompletedAt" IS NOT NULL
                    ),
                    0
                )::double precision,
                COALESCE(
                    percentile_cont(0.99) WITHIN GROUP (
                        ORDER BY EXTRACT(EPOCH FROM (job."CompletedAt" - job."StartedAt")) * 1000
                    ) FILTER (
                        WHERE job."Status" = @CompletedStatus
                          AND job."StartedAt" IS NOT NULL
                          AND job."CompletedAt" IS NOT NULL
                    ),
                    0
                )::double precision
            FROM reporting.tax_report_jobs AS job
            WHERE job."Id" = ANY(@JobIds);
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("CompletedStatus", CompletedStatus);
        command.Parameters.AddWithValue("JobIds", jobIds);

        await using NpgsqlDataReader reader =
            await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);

        return new JobLatencyPercentiles(
            reader.GetDouble(0),
            reader.GetDouble(1),
            reader.GetDouble(2));
    }

    private static async Task<int> RemovePendingJobsAsync(
        NpgsqlConnection connection,
        long[] jobIds,
        CancellationToken cancellationToken)
    {
        const string sql = """
            DELETE FROM reporting.tax_report_jobs
            WHERE "Id" = ANY(@JobIds)
              AND "Status" = @PendingStatus;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("JobIds", jobIds);
        command.Parameters.AddWithValue("PendingStatus", PendingStatus);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> ReadDocumentCountAsync(
        NpgsqlConnection connection,
        long taxReportId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT count(*)
            FROM reporting.tax_report_documents
            WHERE "TaxReportId" = @TaxReportId;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("TaxReportId", taxReportId);
        object? value = await command.ExecuteScalarAsync(cancellationToken);
        return checked(Convert.ToInt32(value, CultureInfo.InvariantCulture));
    }

    private static void WriteResult(
        WorkerPdfBenchmarkResult result,
        int timeoutSeconds)
    {
        double targetPdfsPerSecond =
            result.Requested / (double)timeoutSeconds;

        ConsoleUi.WriteLine();
        ConsoleUi.WriteLine("============================================================");
        ConsoleUi.WriteLine(result.Passed
            ? "[SUCCESS] Worker PDF benchmark passed."
            : "[ERROR] Worker PDF benchmark failed.");
        ConsoleUi.WriteLine($"Requested:       {result.Requested:N0}");
        ConsoleUi.WriteLine($"Completed:       {result.Completed:N0}");
        ConsoleUi.WriteLine($"Failed:          {result.Failed:N0}");
        ConsoleUi.WriteLine($"Still processing:{result.Processing,10:N0}");
        ConsoleUi.WriteLine($"Still pending:   {result.Pending,10:N0}");
        ConsoleUi.WriteLine($"Elapsed:         {result.Elapsed.TotalSeconds,10:F3} s");
        ConsoleUi.WriteLine($"Throughput:      {result.PdfsPerSecond,10:F2} PDFs/s");
        ConsoleUi.WriteLine(
            result.Passed
                ? $"[SUCCESS] Actual vs target: {result.PdfsPerSecond:F2} PDFs/s vs {targetPdfsPerSecond:F2} PDFs/s"
                : $"[ERROR] Actual vs target: {result.PdfsPerSecond:F2} PDFs/s vs {targetPdfsPerSecond:F2} PDFs/s");
        if (result.RequiredDistinctDocuments)
        {
            ConsoleUi.WriteLine(
                result.NewDistinctDocuments >= result.Requested
                    ? $"[SUCCESS] New timestamp-signed outputs: {result.NewDistinctDocuments:N0}/{result.Requested:N0}"
                    : $"[ERROR] New timestamp-signed outputs: {result.NewDistinctDocuments:N0}/{result.Requested:N0}");
        }
        ConsoleUi.WriteLine($"Worker p50:      {result.P50Milliseconds,10:F2} ms");
        ConsoleUi.WriteLine($"Worker p95:      {result.P95Milliseconds,10:F2} ms");
        ConsoleUi.WriteLine($"Worker p99:      {result.P99Milliseconds,10:F2} ms");
        ConsoleUi.WriteLine($"Target:          {result.Requested:N0} PDFs in <= {timeoutSeconds:N0} s");
        ConsoleUi.WriteLine("============================================================");
    }

    private sealed record JobCounts(
        int Pending,
        int Processing,
        int Completed,
        int Failed);

    private sealed record JobLatencyPercentiles(
        double P50Milliseconds,
        double P95Milliseconds,
        double P99Milliseconds);
}
