using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nordiska.Modules.Reporting.Application;
using Nordiska.Reporting.Worker.Execution;
using Nordiska.Reporting.Worker.JobHandlers.AccountStatements;
using Nordiska.Reporting.Worker.JobHandlers.TaxReports;
using Nordiska.Reporting.Worker.Lifecycle;
using Nordiska.Reporting.Worker.Scheduling;
using ReportingWorker = Nordiska.Reporting.Worker.Lifecycle.Worker;

namespace Nordiska.Reporting.Worker.Tests;

public class WorkerLoopTests
{
    private static readonly TimeSpan SignalTimeout = TimeSpan.FromSeconds(10);

    private readonly FakeTaxReportJobs _taxJobs = new();
    private readonly FakeAccountStatementJobs _statementJobs = new();
    private readonly FakeTaxReportCompletion _taxCompletion = new();
    private readonly FakeAccountStatementCompletion _statementCompletion = new();
    private readonly FakeDocumentStorage _storage = new();

    private static TaskCompletionSource<bool> NewSignal()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private ReportingWorker CreateWorker(int pollIntervalSeconds)
    {
        var options = new TaxReportWorkerOptions
        {
            Concurrency = 1,
            PollIntervalSeconds = pollIntervalSeconds
        };

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(DateTimeOffset.UtcNow));
        services.AddSingleton<IOptions<TaxReportWorkerOptions>>(Options.Create(options));
        services.AddSingleton<ITaxReportJobRepository>(_taxJobs);
        services.AddSingleton<ITaxReportRenderPipeline>(new FakeTaxReportRenderer());
        services.AddSingleton<ITaxReportCompletionRepository>(_taxCompletion);
        services.AddSingleton<IAccountStatementJobRepository>(_statementJobs);
        services.AddSingleton<IAccountStatementRenderPipeline>(new FakeAccountStatementRenderer());
        services.AddSingleton<IAccountStatementCompletionRepository>(_statementCompletion);
        services.AddSingleton<IReportDocumentStorage>(_storage);
        services.AddScoped<TaxReportJobProcessor>();
        services.AddScoped<AccountStatementJobProcessor>();

        var provider = services.BuildServiceProvider();

        var lifecycle = new WorkerLifecycle();
        var wakeSignal = new ReportingJobWakeSignal(options.Concurrency);
        var scheduler = new ReportingJobScheduler(
            provider.GetRequiredService<IServiceScopeFactory>(),
            wakeSignal,
            lifecycle,
            Options.Create(options));
        var loop = new WorkerLoop(
            scheduler,
            lifecycle,
            NullLogger<WorkerLoop>.Instance);

        return new ReportingWorker(
            loop,
            lifecycle,
            wakeSignal,
            Options.Create(options),
            NullLogger<ReportingWorker>.Instance);
    }

    [Fact]
    public async Task NoPendingWork_PollsOncePerInterval()
    {
        var polled = NewSignal();
        _statementJobs.OnClaim = _ =>
        {
            polled.TrySetResult(true);
            return null;
        };
        var worker = CreateWorker(pollIntervalSeconds: 30);

        await worker.StartAsync(CancellationToken.None);
        await polled.Task.WaitAsync(SignalTimeout);
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(1, _taxJobs.ClaimCalls);
        Assert.Equal(1, _statementJobs.ClaimCalls);
        Assert.True(worker.ExecuteTask!.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task ProcessedTaxReport_LoopsAgainWithoutWaiting()
    {
        var secondRound = NewSignal();
        _taxJobs.OnClaim = call => call == 1 ? new ClaimedTaxReportJob(5, 21, 1) : null;
        _statementJobs.OnClaim = call =>
        {
            if (call == 2)
            {
                secondRound.TrySetResult(true);
            }

            return null;
        };
        var worker = CreateWorker(pollIntervalSeconds: 30);

        await worker.StartAsync(CancellationToken.None);
        await secondRound.Task.WaitAsync(SignalTimeout);
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(2, _taxJobs.ClaimCalls);
        Assert.Single(_taxCompletion.Completed);
        Assert.Single(_storage.Saved);
        Assert.Empty(_statementCompletion.Completed);
    }

    [Fact]
    public async Task ProcessedAccountStatement_LoopsAgainWithoutWaiting()
    {
        var secondRound = NewSignal();
        _statementJobs.OnClaim = call =>
        {
            if (call == 2)
            {
                secondRound.TrySetResult(true);
            }

            return call == 1 ? new ClaimedAccountStatementJob(8, 33, 1) : null;
        };
        var worker = CreateWorker(pollIntervalSeconds: 30);

        await worker.StartAsync(CancellationToken.None);
        await secondRound.Task.WaitAsync(SignalTimeout);
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(2, _statementJobs.ClaimCalls);
        Assert.Single(_statementCompletion.Completed);
        Assert.Empty(_taxCompletion.Completed);
    }

    [Fact]
    public async Task LoopFailure_IsLoggedAndPollingContinues()
    {
        var secondAttempt = NewSignal();
        _taxJobs.OnClaim = call =>
        {
            if (call == 2)
            {
                secondAttempt.TrySetResult(true);
            }

            throw new InvalidOperationException("database unavailable");
        };
        // A zero interval would never yield with these synchronous fakes and StartAsync would hang.
        var worker = CreateWorker(pollIntervalSeconds: 1);

        await worker.StartAsync(CancellationToken.None);
        await secondAttempt.Task.WaitAsync(SignalTimeout);
        await worker.StopAsync(CancellationToken.None);

        Assert.True(_taxJobs.ClaimCalls >= 2);
        Assert.Equal(0, _statementJobs.ClaimCalls);
    }
}

public class TaxReportWorkerOptionsTests
{
    [Fact]
    public void Defaults_AreConservative()
    {
        var options = new TaxReportWorkerOptions();

        Assert.Equal(30, options.PollIntervalSeconds);
        Assert.Equal(5, options.LeaseDurationMinutes);
        Assert.Equal(30, options.RetryDelaySeconds);
        Assert.Equal(3, options.MaximumAttempts);
    }
}
