using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nordiska.Reporting.Worker.Execution;
using Nordiska.Reporting.Worker.Lifecycle;
using Nordiska.Reporting.Worker.Scheduling;
using ReportingWorker = Nordiska.Reporting.Worker.Lifecycle.Worker;
using Xunit.Abstractions;

namespace Nordiska.Reporting.Worker.Tests;

public sealed class WorkerPoolTests
{
    private readonly ITestOutputHelper _output;

    public WorkerPoolTests(ITestOutputHelper output) =>
        _output = output;

    [Fact]
    public async Task StartAsync_StartsConfiguredNumberOfUniqueWorkerSlots()
    {
        const int concurrency = 4;
        var workerLoop = new RecordingWorkerLoop(concurrency);
        var worker = new ReportingWorker(
            workerLoop,
            new WorkerLifecycle(),
            new NoopWakeSignal(),
            Options.Create(new TaxReportWorkerOptions
            {
                Concurrency = concurrency
            }),
            NullLogger<ReportingWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);

        string[] workerIds = await workerLoop.WaitForWorkerIdsAsync();

        Assert.Equal(concurrency, workerIds.Length);
        Assert.Equal(concurrency, workerIds.Distinct().Count());

        workerLoop.Complete();
        await worker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public void Constructor_ZeroConcurrency_ThrowsArgumentOutOfRangeException()
    {
        var workerLoop = new RecordingWorkerLoop(expectedWorkers: 1);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ReportingWorker(
                workerLoop,
                new WorkerLifecycle(),
                new NoopWakeSignal(),
                Options.Create(new TaxReportWorkerOptions
                {
                    Concurrency = 0
                }),
                NullLogger<ReportingWorker>.Instance));
    }

    [Fact]
    public async Task StopAsync_WaitsForInFlightWorkBeforeCancellingItsToken()
    {
        var workerLoop = new InFlightWorkerLoop();
        var lifecycle = new WorkerLifecycle();
        var logger = new OutputLogger<ReportingWorker>(_output);
        var worker = new ReportingWorker(
            workerLoop,
            lifecycle,
            new NoopWakeSignal(),
            Options.Create(new TaxReportWorkerOptions { Concurrency = 1 }),
            logger);

        await worker.StartAsync(CancellationToken.None);
        await workerLoop.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Task stopping = worker.StopAsync(CancellationToken.None);
        try
        {
            Assert.False(stopping.IsCompleted);
            Assert.False(workerLoop.WorkToken.IsCancellationRequested);
            Assert.Equal(WorkerLifecycleState.Draining, lifecycle.State);
        }
        finally
        {
            workerLoop.Finish.TrySetResult();
            await stopping.WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.Equal(WorkerLifecycleState.Stopped, lifecycle.State);
        Assert.Contains(logger.Messages,
            message => message.Contains("is draining.",
                StringComparison.Ordinal));
        Assert.Contains(logger.Messages,
            message => message.Contains("stopped.",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task StopAsync_AfterDrainTimeout_CancelsWorkerSlots()
    {
        var workerLoop = new RecordingWorkerLoop(expectedWorkers: 1);
        var worker = new ReportingWorker(
            workerLoop,
            new WorkerLifecycle(),
            new NoopWakeSignal(),
            Options.Create(new TaxReportWorkerOptions
            {
                Concurrency = 1,
                DrainTimeoutSeconds = 1
            }),
            NullLogger<ReportingWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await workerLoop.WaitForWorkerIdsAsync();

        await worker.StopAsync(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(3));
    }

    private sealed class InFlightWorkerLoop : IWorkerLoop
    {
        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Finish { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public CancellationToken WorkToken { get; private set; }

        public async Task RunAsync(
            string workerId,
            CancellationToken stoppingToken)
        {
            WorkToken = stoppingToken;
            Started.TrySetResult();
            await Finish.Task;
        }
    }

    private sealed class RecordingWorkerLoop : IWorkerLoop
    {
        private readonly int _expectedWorkers;
        private readonly ConcurrentBag<string> _workerIds = [];
        private readonly TaskCompletionSource<string[]> _allWorkersStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _finish =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public RecordingWorkerLoop(int expectedWorkers)
        {
            _expectedWorkers = expectedWorkers;
        }

        public async Task RunAsync(
            string workerId,
            CancellationToken stoppingToken)
        {
            _workerIds.Add(workerId);

            if (_workerIds.Count == _expectedWorkers)
                _allWorkersStarted.TrySetResult(_workerIds.ToArray());

            try
            {
                await _finish.Task.WaitAsync(stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            { }
        }

        public Task<string[]> WaitForWorkerIdsAsync() =>
            _allWorkersStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        public void Complete() => _finish.TrySetResult();
    }

    private sealed class NoopWakeSignal : IReportingJobWakeSignal
    {
        public void Pulse() { }

        public Task WaitAsync(
            TimeSpan fallbackDelay,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class OutputLogger<T> : ILogger<T>
    {
        private readonly ITestOutputHelper _output;

        public OutputLogger(ITestOutputHelper output) =>
            _output = output;

        public ConcurrentQueue<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            string message = formatter(state, exception);
            Messages.Enqueue(message);
            _output.WriteLine(message);
        }
    }
}
