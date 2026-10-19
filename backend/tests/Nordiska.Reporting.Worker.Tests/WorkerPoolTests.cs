using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nordiska.Reporting.Worker.TaxReports;

namespace Nordiska.Reporting.Worker.Tests;

public sealed class WorkerPoolTests
{
    [Fact]
    public async Task StartAsync_StartsConfiguredNumberOfUniqueWorkerSlots()
    {
        const int concurrency = 4;
        var workerLoop = new RecordingWorkerLoop(concurrency);
        var worker = new Worker(
            workerLoop,
            Options.Create(new TaxReportWorkerOptions
            {
                Concurrency = concurrency
            }),
            NullLogger<Worker>.Instance);

        await worker.StartAsync(CancellationToken.None);

        string[] workerIds = await workerLoop.WaitForWorkerIdsAsync();

        Assert.Equal(concurrency, workerIds.Length);
        Assert.Equal(concurrency, workerIds.Distinct().Count());

        await worker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public void Constructor_ZeroConcurrency_ThrowsArgumentOutOfRangeException()
    {
        var workerLoop = new RecordingWorkerLoop(expectedWorkers: 1);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Worker(
                workerLoop,
                Options.Create(new TaxReportWorkerOptions
                {
                    Concurrency = 0
                }),
                NullLogger<Worker>.Instance));
    }

    private sealed class RecordingWorkerLoop : IWorkerLoop
    {
        private readonly int _expectedWorkers;
        private readonly ConcurrentBag<string> _workerIds = [];
        private readonly TaskCompletionSource<string[]> _allWorkersStarted =
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
                await Task.Delay(
                    Timeout.InfiniteTimeSpan,
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            { }
        }

        public Task<string[]> WaitForWorkerIdsAsync() =>
            _allWorkersStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
