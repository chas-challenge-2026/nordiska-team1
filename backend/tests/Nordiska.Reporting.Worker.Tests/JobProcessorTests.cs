using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nordiska.Modules.Reporting.Application;
using Nordiska.Reporting.Worker.Execution;
using Nordiska.Reporting.Worker.JobHandlers.AccountStatements;
using Nordiska.Reporting.Worker.JobHandlers.TaxReports;
using Nordiska.Reporting.Worker.Scheduling;

namespace Nordiska.Reporting.Worker.Tests;

public class TaxReportJobProcessorTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeTaxReportJobs _jobs = new();
    private readonly FakeTaxReportRenderer _renderer = new();
    private readonly FakeDocumentStorage _storage = new();
    private readonly FakeTaxReportCompletion _completion = new();
    private readonly TaxReportWorkerOptions _options = new()
    {
        LeaseDurationMinutes = 9,
        RetryDelaySeconds = 45,
        MaximumAttempts = 4
    };

    private TaxReportJobProcessor CreateProcessor()
        => new(
            _jobs,
            _renderer,
            _storage,
            _completion,
            new FixedTimeProvider(Now),
            Options.Create(_options),
            NullLogger<TaxReportJobProcessor>.Instance);

    private static string HashOf(byte[] bytes)
        => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    [Fact]
    public async Task ProcessNext_NoJobAvailable_ReturnsFalseAndDoesNothing()
    {
        var processed = await CreateProcessor().ProcessNextAsync("worker-1", CancellationToken.None);

        Assert.False(processed);
        Assert.Equal(1, _jobs.ClaimCalls);
        Assert.Empty(_storage.Saved);
        Assert.Empty(_completion.Completed);
        Assert.Empty(_jobs.Failures);
    }

    [Fact]
    public async Task ProcessNext_ClaimsJobWithWorkerIdCurrentTimeAndLease()
    {
        await CreateProcessor().ProcessNextAsync("worker-1", CancellationToken.None);

        Assert.Equal("worker-1", _jobs.ClaimedWorkerId);
        Assert.Equal(Now, _jobs.ClaimedAt);
        Assert.Equal(TimeSpan.FromMinutes(9), _jobs.LeaseDuration);
    }

    [Fact]
    public async Task ProcessNext_Success_StoresPdfUnderHashedKeyAndCompletesJob()
    {
        _jobs.OnClaim = _ => new ClaimedTaxReportJob(JobId: 5, TaxReportId: 21, AttemptNumber: 1);
        var hash = HashOf(ReportingTestData.ValidPdf);

        var processed = await CreateProcessor().ProcessNextAsync("worker-1", CancellationToken.None);

        Assert.True(processed);

        var saved = Assert.Single(_storage.Saved);
        Assert.Equal($"tax-reports/21/{hash}.pdf", saved.Key);
        Assert.Equal(hash, saved.Hash);
        Assert.Equal(ReportingTestData.ValidPdf, saved.Content);

        var completed = Assert.Single(_completion.Completed);
        Assert.Equal(5, completed.JobId);
        Assert.Equal("worker-1", completed.WorkerId);
        Assert.Equal(Now, completed.CompletedAt);
        Assert.Equal(21, completed.Document.TaxReportId);
        Assert.Equal("arsbesked-2025-1234.pdf", completed.Document.FileName);
        Assert.Equal(saved.Key, completed.Document.StorageKey);
        Assert.Equal(ReportingTestData.ValidPdf.Length, completed.Document.ByteLength);
        Assert.Equal(hash, completed.Document.Sha256Hash);
        Assert.Empty(_jobs.Failures);
    }

    [Fact]
    public async Task ProcessNext_RenderFails_MarksJobFailedWithRetrySettings()
    {
        _jobs.OnClaim = _ => new ClaimedTaxReportJob(5, 21, 1);
        _renderer.OnRender = _ => throw new InvalidDataException("bad snapshot");

        var processed = await CreateProcessor().ProcessNextAsync("worker-1", CancellationToken.None);

        Assert.True(processed);
        var failure = Assert.Single(_jobs.Failures);
        Assert.Equal(5, failure.JobId);
        Assert.Equal("worker-1", failure.WorkerId);
        Assert.Equal("bad snapshot", failure.Error);
        Assert.Equal(Now, failure.FailedAt);
        Assert.Equal(TimeSpan.FromSeconds(45), failure.RetryDelay);
        Assert.Equal(4, failure.MaximumAttempts);
        Assert.Empty(_storage.Saved);
        Assert.Empty(_completion.Completed);
    }

    [Fact]
    public async Task ProcessNext_StorageFails_MarksJobFailedAndDoesNotComplete()
    {
        _jobs.OnClaim = _ => new ClaimedTaxReportJob(5, 21, 1);
        _storage.ThrowOnSave = new IOException("disk full");

        var processed = await CreateProcessor().ProcessNextAsync("worker-1", CancellationToken.None);

        Assert.True(processed);
        Assert.Equal("disk full", Assert.Single(_jobs.Failures).Error);
        Assert.Empty(_completion.Completed);
    }

    [Fact]
    public async Task ProcessNext_CancelledWhileRendering_PropagatesInsteadOfFailingJob()
    {
        using var cts = new CancellationTokenSource();
        _jobs.OnClaim = _ => new ClaimedTaxReportJob(5, 21, 1);
        _renderer.OnRender = _ =>
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateProcessor().ProcessNextAsync("worker-1", cts.Token));

        Assert.Empty(_jobs.Failures);
    }
}

public class AccountStatementJobProcessorTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeAccountStatementJobs _jobs = new();
    private readonly FakeAccountStatementRenderer _renderer = new();
    private readonly FakeDocumentStorage _storage = new();
    private readonly FakeAccountStatementCompletion _completion = new();
    private readonly TaxReportWorkerOptions _options = new()
    {
        LeaseDurationMinutes = 9,
        RetryDelaySeconds = 45,
        MaximumAttempts = 4
    };

    private AccountStatementJobProcessor CreateProcessor()
        => new(
            _jobs,
            _renderer,
            _storage,
            _completion,
            new FixedTimeProvider(Now),
            Options.Create(_options),
            NullLogger<AccountStatementJobProcessor>.Instance);

    private static string HashOf(byte[] bytes)
        => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    [Fact]
    public async Task ProcessNext_NoJobAvailable_ReturnsFalseAndDoesNothing()
    {
        var processed = await CreateProcessor().ProcessNextAsync("worker-1", CancellationToken.None);

        Assert.False(processed);
        Assert.Equal(1, _jobs.ClaimCalls);
        Assert.Empty(_storage.Saved);
        Assert.Empty(_completion.Completed);
        Assert.Empty(_jobs.Failures);
    }

    [Fact]
    public async Task ProcessNext_ClaimsJobWithWorkerIdCurrentTimeAndLease()
    {
        await CreateProcessor().ProcessNextAsync("worker-1", CancellationToken.None);

        Assert.Equal("worker-1", _jobs.ClaimedWorkerId);
        Assert.Equal(Now, _jobs.ClaimedAt);
        Assert.Equal(TimeSpan.FromMinutes(9), _jobs.LeaseDuration);
    }

    [Fact]
    public async Task ProcessNext_Success_StoresPdfUnderHashedKeyAndCompletesJob()
    {
        _jobs.OnClaim = _ => new ClaimedAccountStatementJob(JobId: 8, AccountStatementId: 33, AttemptNumber: 1);
        var hash = HashOf(ReportingTestData.ValidPdf);

        var processed = await CreateProcessor().ProcessNextAsync("worker-2", CancellationToken.None);

        Assert.True(processed);

        var saved = Assert.Single(_storage.Saved);
        Assert.Equal($"account-statements/33/{hash}.pdf", saved.Key);
        Assert.Equal(hash, saved.Hash);

        var completed = Assert.Single(_completion.Completed);
        Assert.Equal(8, completed.JobId);
        Assert.Equal("worker-2", completed.WorkerId);
        Assert.Equal(Now, completed.CompletedAt);
        Assert.Equal(33, completed.Document.AccountStatementId);
        Assert.Equal("kontoutdrag.pdf", completed.Document.FileName);
        Assert.Equal(saved.Key, completed.Document.StorageKey);
        Assert.Equal(ReportingTestData.ValidPdf.Length, completed.Document.ByteLength);
        Assert.Equal(hash, completed.Document.Sha256Hash);
        Assert.Empty(_jobs.Failures);
    }

    [Fact]
    public async Task ProcessNext_RenderFails_MarksJobFailedWithRetrySettings()
    {
        _jobs.OnClaim = _ => new ClaimedAccountStatementJob(8, 33, 1);
        _renderer.OnRender = _ => throw new InvalidDataException("bad snapshot");

        var processed = await CreateProcessor().ProcessNextAsync("worker-2", CancellationToken.None);

        Assert.True(processed);
        var failure = Assert.Single(_jobs.Failures);
        Assert.Equal(8, failure.JobId);
        Assert.Equal("worker-2", failure.WorkerId);
        Assert.Equal("bad snapshot", failure.Error);
        Assert.Equal(Now, failure.FailedAt);
        Assert.Equal(TimeSpan.FromSeconds(45), failure.RetryDelay);
        Assert.Equal(4, failure.MaximumAttempts);
        Assert.Empty(_storage.Saved);
        Assert.Empty(_completion.Completed);
    }

    [Fact]
    public async Task ProcessNext_StorageFails_MarksJobFailedAndDoesNotComplete()
    {
        _jobs.OnClaim = _ => new ClaimedAccountStatementJob(8, 33, 1);
        _storage.ThrowOnSave = new IOException("disk full");

        var processed = await CreateProcessor().ProcessNextAsync("worker-2", CancellationToken.None);

        Assert.True(processed);
        Assert.Equal("disk full", Assert.Single(_jobs.Failures).Error);
        Assert.Empty(_completion.Completed);
    }

    [Fact]
    public async Task ProcessNext_CancelledWhileRendering_PropagatesInsteadOfFailingJob()
    {
        using var cts = new CancellationTokenSource();
        _jobs.OnClaim = _ => new ClaimedAccountStatementJob(8, 33, 1);
        _renderer.OnRender = _ =>
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateProcessor().ProcessNextAsync("worker-2", cts.Token));

        Assert.Empty(_jobs.Failures);
    }
}
