using Nordiska.Modules.Reporting.Application;
using Nordiska.Modules.Reporting.Domain;
using Nordiska.Modules.Reporting.PdfGeneration;
using Nordiska.Reporting.Worker.AccountStatements;
using Nordiska.Reporting.Worker.TaxReports;

namespace Nordiska.Reporting.Worker.Tests;

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal sealed record JobFailure(
    long JobId,
    string WorkerId,
    string Error,
    DateTimeOffset FailedAt,
    TimeSpan RetryDelay,
    int MaximumAttempts);

internal sealed record JobCompletion<TDocument>(
    long JobId,
    string WorkerId,
    TDocument Document,
    DateTimeOffset CompletedAt);

internal sealed record SavedDocument(string Key, byte[] Content, string Hash);

internal sealed class FakeTaxReportJobs : ITaxReportJobRepository
{
    public Func<int, ClaimedTaxReportJob?> OnClaim { get; set; } = _ => null;

    public int ClaimCalls { get; private set; }

    public string? ClaimedWorkerId { get; private set; }

    public DateTimeOffset? ClaimedAt { get; private set; }

    public TimeSpan? LeaseDuration { get; private set; }

    public List<JobFailure> Failures { get; } = [];

    public Task<ClaimedTaxReportJob?> ClaimNextAsync(
        string workerId,
        DateTimeOffset now,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        ClaimCalls++;
        ClaimedWorkerId = workerId;
        ClaimedAt = now;
        LeaseDuration = leaseDuration;
        return Task.FromResult(OnClaim(ClaimCalls));
    }

    public Task MarkFailedAsync(
        long jobId,
        string workerId,
        string error,
        DateTimeOffset failedAt,
        TimeSpan retryDelay,
        int maximumAttempts,
        CancellationToken cancellationToken)
    {
        Failures.Add(new JobFailure(jobId, workerId, error, failedAt, retryDelay, maximumAttempts));
        return Task.CompletedTask;
    }
}

internal sealed class FakeAccountStatementJobs : IAccountStatementJobRepository
{
    public Func<int, ClaimedAccountStatementJob?> OnClaim { get; set; } = _ => null;

    public int ClaimCalls { get; private set; }

    public string? ClaimedWorkerId { get; private set; }

    public DateTimeOffset? ClaimedAt { get; private set; }

    public TimeSpan? LeaseDuration { get; private set; }

    public List<JobFailure> Failures { get; } = [];

    public Task<ClaimedAccountStatementJob?> ClaimNextAsync(
        string workerId,
        DateTimeOffset now,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        ClaimCalls++;
        ClaimedWorkerId = workerId;
        ClaimedAt = now;
        LeaseDuration = leaseDuration;
        return Task.FromResult(OnClaim(ClaimCalls));
    }

    public Task MarkFailedAsync(
        long jobId,
        string workerId,
        string error,
        DateTimeOffset failedAt,
        TimeSpan retryDelay,
        int maximumAttempts,
        CancellationToken cancellationToken)
    {
        Failures.Add(new JobFailure(jobId, workerId, error, failedAt, retryDelay, maximumAttempts));
        return Task.CompletedTask;
    }
}

internal sealed class FakeDocumentStorage : IReportDocumentStorage
{
    public Exception? ThrowOnSave { get; set; }

    public List<SavedDocument> Saved { get; } = [];

    public Task SaveAsync(
        string storageKey,
        ReadOnlyMemory<byte> content,
        string expectedSha256Hash,
        CancellationToken cancellationToken)
    {
        if (ThrowOnSave is not null)
        {
            throw ThrowOnSave;
        }

        Saved.Add(new SavedDocument(storageKey, content.ToArray(), expectedSha256Hash));
        return Task.CompletedTask;
    }

    public Task<byte[]> ReadAsync(
        string storageKey,
        string expectedSha256Hash,
        CancellationToken cancellationToken)
        => throw new NotSupportedException();
}

internal sealed class FakeTaxReportCompletion : ITaxReportCompletionRepository
{
    public List<JobCompletion<CompletedReportDocument>> Completed { get; } = [];

    public Task CompleteAsync(
        long jobId,
        string workerId,
        CompletedReportDocument document,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken)
    {
        Completed.Add(new JobCompletion<CompletedReportDocument>(jobId, workerId, document, completedAt));
        return Task.CompletedTask;
    }
}

internal sealed class FakeAccountStatementCompletion : IAccountStatementCompletionRepository
{
    public List<JobCompletion<CompletedAccountStatementDocument>> Completed { get; } = [];

    public Task CompleteAsync(
        long jobId,
        string workerId,
        CompletedAccountStatementDocument document,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken)
    {
        Completed.Add(new JobCompletion<CompletedAccountStatementDocument>(jobId, workerId, document, completedAt));
        return Task.CompletedTask;
    }
}

internal sealed class FakeTaxReportRenderer : ITaxReportRenderPipeline
{
    public Func<long, RenderedTaxReport> OnRender { get; set; } = id => new RenderedTaxReport(
        id, 7, $"annual_tax_report_{id}", "arsbesked-2025-1234.pdf", ReportingTestData.ValidPdf);

    public Task<RenderedTaxReport> RenderAsync(long taxReportId, CancellationToken cancellationToken)
        => Task.FromResult(OnRender(taxReportId));
}

internal sealed class FakeAccountStatementRenderer : IAccountStatementRenderPipeline
{
    public Func<long, RenderedAccountStatement> OnRender { get; set; } = id => new RenderedAccountStatement(
        id, 7, $"account_statement_{id}", "kontoutdrag.pdf", ReportingTestData.ValidPdf);

    public Task<RenderedAccountStatement> RenderAsync(long accountStatementId, CancellationToken cancellationToken)
        => Task.FromResult(OnRender(accountStatementId));
}

internal sealed class FakeTaxReports : IAnnualTaxReportRepository
{
    public TaxReport? Report { get; set; }

    public Task<TaxReport?> GetByIdAsync(long taxReportId, CancellationToken cancellationToken)
        => Task.FromResult(Report);

    public Task<TaxReport?> GetByAccountAndYearAsync(long customerId, long accountId, int taxYear, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task<TaxReportJob?> GetLatestJobAsync(long taxReportId, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task<(TaxReport Report, TaxReportJob Job)> CreateReportAndJobAsync(TaxReport report, DateTimeOffset jobCreatedAt, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task<TaxReportJob> CreateJobAsync(long taxReportId, DateTimeOffset createdAt, CancellationToken cancellationToken)
        => throw new NotSupportedException();
}

internal sealed class FakeAccountStatements : IAccountStatementRepository
{
    public AccountStatement? Statement { get; set; }

    public Task<AccountStatement?> GetByIdAsync(long accountStatementId, CancellationToken cancellationToken)
        => Task.FromResult(Statement);

    public Task<InFlightAccountStatement?> GetInFlightAsync(long customerId, long accountId, DateOnly periodStart, DateOnly periodEnd, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task<(AccountStatement Statement, AccountStatementJob Job)> CreateStatementAndJobAsync(AccountStatement statement, DateTimeOffset jobCreatedAt, CancellationToken cancellationToken)
        => throw new NotSupportedException();
}

internal sealed class FakeTaxVerifier : ITaxReportSnapshotVerifier
{
    public Exception? Throws { get; set; }

    public int Calls { get; private set; }

    public void Verify(TaxReport report)
    {
        Calls++;
        if (Throws is not null)
        {
            throw Throws;
        }
    }
}

internal sealed class FakeStatementVerifier : IAccountStatementSnapshotVerifier
{
    public Exception? Throws { get; set; }

    public int Calls { get; private set; }

    public void Verify(AccountStatement statement)
    {
        Calls++;
        if (Throws is not null)
        {
            throw Throws;
        }
    }
}

internal sealed class FakePdfGenerator : IPdfBatchGenerator
{
    public GeneratedPdfBatch? Batch { get; set; }

    public string? LastJson { get; private set; }

    public int Calls { get; private set; }

    public GeneratedPdfBatch GeneratePdfBatch(string json)
    {
        Calls++;
        LastJson = json;
        return Batch ?? throw new InvalidOperationException("No batch configured.");
    }
}

internal static class ReportingTestData
{
    public static byte[] ValidPdf { get; } = "%PDF-1.7 fake document"u8.ToArray();

    public static GeneratedPdfBatch BatchFor(ulong customerId, string documentId, byte[] pdf)
        => new(customerId, new Dictionary<string, byte[]> { [documentId] = pdf });

    public static TaxReport NewTaxReport(long customerId = 7)
        => TaxReport.Create(
            customerId,
            accountId: 1,
            taxYear: 2025,
            totalInterestMinor: 123456,
            taxDeductedMinor: 37037,
            currency: "SEK",
            customerName: "Anna Svensson",
            accountNumber: "1234-567",
            accountName: "Sparkonto",
            createdAt: new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.FromHours(1)),
            schemaVersion: "1.0",
            payloadHash: "hash");

    public static AccountStatement NewStatement(
        long customerId = 7,
        params AccountStatementEntryData[] entries)
        => AccountStatement.Create(
            customerId,
            accountId: 1,
            periodStart: new DateOnly(2026, 1, 1),
            periodEnd: new DateOnly(2026, 1, 31),
            customerName: "Anna Svensson",
            accountNumber: "1234-567",
            accountName: "Sparkonto",
            currency: "SEK",
            openingBalanceMinor: 100000,
            closingBalanceMinor: 145000,
            snapshotAt: new DateTimeOffset(2026, 2, 1, 8, 0, 0, TimeSpan.Zero),
            schemaVersion: "1.0",
            payloadHash: "hash",
            entries: entries);
}
