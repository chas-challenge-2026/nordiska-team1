using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nordiska.Modules.Reporting.Application;
using Nordiska.Reporting.Worker.Execution;
using Nordiska.Reporting.Worker.JobHandlers.AccountStatements;
using Nordiska.Reporting.Worker.JobHandlers.TaxReports;
using Nordiska.Reporting.Worker.Lifecycle;
using Nordiska.Reporting.Worker.Scheduling;

namespace Nordiska.Reporting.Worker.Tests;

public sealed class WorkerLoopWakeTests
{
    [Fact]
    public async Task RunCycleAsync_ProbesTaxThenAccountStatement_AndWaitsWhenIdle()
    {
        var probes = new List<string>();
        var wakeSignal = new ImmediateWakeSignal();
        var lifecycle = StartedLifecycle();
        var options = Options.Create(new TaxReportWorkerOptions
        {
            PollIntervalSeconds = 30
        });

        var services = new ServiceCollection();
        services.AddSingleton<IOptions<TaxReportWorkerOptions>>(options);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ITaxReportJobRepository>(
            new EmptyTaxReportJobRepository(() => probes.Add("tax")));
        services.AddSingleton<IAccountStatementJobRepository>(
            new EmptyAccountStatementJobRepository(
                () => probes.Add("account-statement")));
        services.AddSingleton<ITaxReportRenderPipeline>(
            new UnusedTaxReportRenderPipeline());
        services.AddSingleton<IAccountStatementRenderPipeline>(
            new UnusedAccountStatementRenderPipeline());
        services.AddSingleton<IReportDocumentStorage>(
            new UnusedDocumentStorage());
        services.AddSingleton<ITaxReportCompletionRepository>(
            new UnusedTaxReportCompletionRepository());
        services.AddSingleton<IAccountStatementCompletionRepository>(
            new UnusedAccountStatementCompletionRepository());
        services.AddSingleton<ILogger<TaxReportJobProcessor>>(
            NullLogger<TaxReportJobProcessor>.Instance);
        services.AddSingleton<ILogger<AccountStatementJobProcessor>>(
            NullLogger<AccountStatementJobProcessor>.Instance);
        services.AddScoped<TaxReportJobProcessor>();
        services.AddScoped<AccountStatementJobProcessor>();

        await using ServiceProvider provider =
            services.BuildServiceProvider();

        var scheduler = new ReportingJobScheduler(
            provider.GetRequiredService<IServiceScopeFactory>(),
            wakeSignal,
            lifecycle,
            options);

        await scheduler.RunCycleAsync("worker-01", CancellationToken.None);

        Assert.Equal(["tax", "account-statement"], probes);
        Assert.Equal(TimeSpan.FromSeconds(30), wakeSignal.FallbackDelay);
    }

    [Fact]
    public async Task RunCycleAsync_DrainingAfterTaxProbe_DoesNotClaimAccountStatement()
    {
        var probes = new List<string>();
        var lifecycle = StartedLifecycle();
        var wakeSignal = new ImmediateWakeSignal();
        var options = Options.Create(new TaxReportWorkerOptions());
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ITaxReportJobRepository>(
            new EmptyTaxReportJobRepository(() =>
            {
                probes.Add("tax");
                lifecycle.BeginDraining();
            }));
        services.AddSingleton<IAccountStatementJobRepository>(
            new EmptyAccountStatementJobRepository(
                () => probes.Add("account-statement")));
        services.AddSingleton<ITaxReportRenderPipeline>(
            new UnusedTaxReportRenderPipeline());
        services.AddSingleton<IAccountStatementRenderPipeline>(
            new UnusedAccountStatementRenderPipeline());
        services.AddSingleton<IReportDocumentStorage>(
            new UnusedDocumentStorage());
        services.AddSingleton<ITaxReportCompletionRepository>(
            new UnusedTaxReportCompletionRepository());
        services.AddSingleton<IAccountStatementCompletionRepository>(
            new UnusedAccountStatementCompletionRepository());
        services.AddSingleton<ILogger<TaxReportJobProcessor>>(
            NullLogger<TaxReportJobProcessor>.Instance);
        services.AddSingleton<ILogger<AccountStatementJobProcessor>>(
            NullLogger<AccountStatementJobProcessor>.Instance);
        services.AddSingleton<IOptions<TaxReportWorkerOptions>>(options);
        services.AddScoped<TaxReportJobProcessor>();
        services.AddScoped<AccountStatementJobProcessor>();

        await using ServiceProvider provider =
            services.BuildServiceProvider();
        var scheduler = new ReportingJobScheduler(
            provider.GetRequiredService<IServiceScopeFactory>(),
            wakeSignal,
            lifecycle,
            options);

        await scheduler.RunCycleAsync("worker-01", CancellationToken.None);

        Assert.Equal(["tax"], probes);
        Assert.Null(wakeSignal.FallbackDelay);
    }

    [Fact]
    public async Task RunAsync_WhenBothQueuesAreEmpty_WaitsForNotification()
    {
        var wakeSignal = new RecordingWakeSignal();
        var lifecycle = StartedLifecycle();
        var options = Options.Create(new TaxReportWorkerOptions
        {
            PollIntervalSeconds = 30
        });

        var services = new ServiceCollection();
        services.AddSingleton<IOptions<TaxReportWorkerOptions>>(options);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ITaxReportJobRepository>(
            new EmptyTaxReportJobRepository());
        services.AddSingleton<IAccountStatementJobRepository>(
            new EmptyAccountStatementJobRepository());
        services.AddSingleton<ITaxReportRenderPipeline>(
            new UnusedTaxReportRenderPipeline());
        services.AddSingleton<IAccountStatementRenderPipeline>(
            new UnusedAccountStatementRenderPipeline());
        services.AddSingleton<IReportDocumentStorage>(
            new UnusedDocumentStorage());
        services.AddSingleton<ITaxReportCompletionRepository>(
            new UnusedTaxReportCompletionRepository());
        services.AddSingleton<IAccountStatementCompletionRepository>(
            new UnusedAccountStatementCompletionRepository());
        services.AddSingleton<ILogger<TaxReportJobProcessor>>(
            NullLogger<TaxReportJobProcessor>.Instance);
        services.AddSingleton<ILogger<AccountStatementJobProcessor>>(
            NullLogger<AccountStatementJobProcessor>.Instance);
        services.AddScoped<TaxReportJobProcessor>();
        services.AddScoped<AccountStatementJobProcessor>();

        await using ServiceProvider provider =
            services.BuildServiceProvider();

        var loop = new WorkerLoop(
            new ReportingJobScheduler(
                provider.GetRequiredService<IServiceScopeFactory>(),
                wakeSignal,
                lifecycle,
                options),
            lifecycle,
            NullLogger<WorkerLoop>.Instance);

        using var cancellation = new CancellationTokenSource();
        Task running = loop.RunAsync(
            "worker-01",
            cancellation.Token);

        TimeSpan fallbackDelay = await wakeSignal.WaitStarted
            .Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(TimeSpan.FromSeconds(30), fallbackDelay);

        await cancellation.CancelAsync();
        await running.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task RunAsync_WhenQueueProbeThrows_WaitsForNotificationBeforeRetrying()
    {
        var wakeSignal = new RecordingWakeSignal();
        var lifecycle = StartedLifecycle();
        var options = Options.Create(new TaxReportWorkerOptions
        {
            PollIntervalSeconds = 30
        });

        var services = new ServiceCollection();
        services.AddSingleton<IOptions<TaxReportWorkerOptions>>(options);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ITaxReportJobRepository>(
            new ThrowingTaxReportJobRepository());
        services.AddSingleton<IAccountStatementJobRepository>(
            new EmptyAccountStatementJobRepository());
        services.AddSingleton<ITaxReportRenderPipeline>(
            new UnusedTaxReportRenderPipeline());
        services.AddSingleton<IAccountStatementRenderPipeline>(
            new UnusedAccountStatementRenderPipeline());
        services.AddSingleton<IReportDocumentStorage>(
            new UnusedDocumentStorage());
        services.AddSingleton<ITaxReportCompletionRepository>(
            new UnusedTaxReportCompletionRepository());
        services.AddSingleton<IAccountStatementCompletionRepository>(
            new UnusedAccountStatementCompletionRepository());
        services.AddSingleton<ILogger<TaxReportJobProcessor>>(
            NullLogger<TaxReportJobProcessor>.Instance);
        services.AddSingleton<ILogger<AccountStatementJobProcessor>>(
            NullLogger<AccountStatementJobProcessor>.Instance);
        services.AddScoped<TaxReportJobProcessor>();
        services.AddScoped<AccountStatementJobProcessor>();

        await using ServiceProvider provider =
            services.BuildServiceProvider();

        var loop = new WorkerLoop(
            new ReportingJobScheduler(
                provider.GetRequiredService<IServiceScopeFactory>(),
                wakeSignal,
                lifecycle,
                options),
            lifecycle,
            NullLogger<WorkerLoop>.Instance);

        using var cancellation = new CancellationTokenSource();
        Task running = loop.RunAsync(
            "worker-01",
            cancellation.Token);

        try
        {
            TimeSpan fallbackDelay = await wakeSignal.WaitStarted
                .Task.WaitAsync(TimeSpan.FromSeconds(1));

            Assert.Equal(TimeSpan.FromSeconds(30), fallbackDelay);
        }
        finally
        {
            await cancellation.CancelAsync();
            await running.WaitAsync(TimeSpan.FromSeconds(1));
        }
    }

    private static WorkerLifecycle StartedLifecycle()
    {
        var lifecycle = new WorkerLifecycle();
        lifecycle.Start();
        return lifecycle;
    }

    private sealed class RecordingWakeSignal
        : IReportingJobWakeSignal
    {
        public TaskCompletionSource<TimeSpan> WaitStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Pulse()
        { }

        public async Task WaitAsync(
            TimeSpan fallbackDelay,
            CancellationToken cancellationToken)
        {
            WaitStarted.TrySetResult(fallbackDelay);
            await Task.Delay(
                Timeout.InfiniteTimeSpan,
                cancellationToken);
        }
    }

    private sealed class ImmediateWakeSignal : IReportingJobWakeSignal
    {
        public TimeSpan? FallbackDelay { get; private set; }

        public void Pulse()
        { }

        public Task WaitAsync(
            TimeSpan fallbackDelay,
            CancellationToken cancellationToken)
        {
            FallbackDelay = fallbackDelay;
            return Task.CompletedTask;
        }
    }

    private sealed class EmptyTaxReportJobRepository
        : ITaxReportJobRepository
    {
        private readonly Action? _onClaim;

        public EmptyTaxReportJobRepository(Action? onClaim = null)
        {
            _onClaim = onClaim;
        }

        public Task<ClaimedTaxReportJob?> ClaimNextAsync(
            string workerId,
            DateTimeOffset now,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken)
        {
            _onClaim?.Invoke();
            return Task.FromResult<ClaimedTaxReportJob?>(null);
        }

        public Task MarkFailedAsync(
            long jobId,
            string workerId,
            string error,
            DateTimeOffset failedAt,
            TimeSpan retryDelay,
            int maximumAttempts,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class ThrowingTaxReportJobRepository
        : ITaxReportJobRepository
    {
        public Task<ClaimedTaxReportJob?> ClaimNextAsync(
            string workerId,
            DateTimeOffset now,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException(
                "Database unavailable.");

        public Task MarkFailedAsync(
            long jobId,
            string workerId,
            string error,
            DateTimeOffset failedAt,
            TimeSpan retryDelay,
            int maximumAttempts,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class EmptyAccountStatementJobRepository
        : IAccountStatementJobRepository
    {
        private readonly Action? _onClaim;

        public EmptyAccountStatementJobRepository(Action? onClaim = null)
        {
            _onClaim = onClaim;
        }

        public Task<ClaimedAccountStatementJob?> ClaimNextAsync(
            string workerId,
            DateTimeOffset now,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken)
        {
            _onClaim?.Invoke();
            return Task.FromResult<ClaimedAccountStatementJob?>(null);
        }

        public Task MarkFailedAsync(
            long jobId,
            string workerId,
            string error,
            DateTimeOffset failedAt,
            TimeSpan retryDelay,
            int maximumAttempts,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class UnusedTaxReportRenderPipeline
        : ITaxReportRenderPipeline
    {
        public Task<RenderedTaxReport> RenderAsync(
            long taxReportId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class UnusedAccountStatementRenderPipeline
        : IAccountStatementRenderPipeline
    {
        public Task<RenderedAccountStatement> RenderAsync(
            long accountStatementId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class UnusedDocumentStorage
        : IReportDocumentStorage
    {
        public Task SaveAsync(
            string storageKey,
            ReadOnlyMemory<byte> content,
            string expectedSha256Hash,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<byte[]> ReadAsync(
            string storageKey,
            string expectedSha256Hash,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class UnusedTaxReportCompletionRepository
        : ITaxReportCompletionRepository
    {
        public Task CompleteAsync(
            long jobId,
            string workerId,
            CompletedReportDocument document,
            DateTimeOffset completedAt,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class UnusedAccountStatementCompletionRepository
        : IAccountStatementCompletionRepository
    {
        public Task CompleteAsync(
            long jobId,
            string workerId,
            CompletedAccountStatementDocument document,
            DateTimeOffset completedAt,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
