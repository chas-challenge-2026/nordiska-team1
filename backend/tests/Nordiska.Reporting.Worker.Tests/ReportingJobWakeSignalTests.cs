using Nordiska.Reporting.Worker.Scheduling;

namespace Nordiska.Reporting.Worker.Tests;

public sealed class ReportingJobWakeSignalTests
{
    [Fact]
    public async Task Pulse_WakesEveryConfiguredWorkerSlot()
    {
        const int concurrency = 4;
        var signal = new ReportingJobWakeSignal(concurrency);

        Task[] waiters = Enumerable.Range(0, concurrency)
            .Select(_ => signal.WaitAsync(
                Timeout.InfiniteTimeSpan,
                CancellationToken.None))
            .ToArray();

        signal.Pulse();

        await Task.WhenAll(waiters)
            .WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task WaitAsync_WhenNoNotification_ReturnsAfterFallbackDelay()
    {
        var signal = new ReportingJobWakeSignal(workerCount: 1);

        Task wait = signal.WaitAsync(
            TimeSpan.FromMilliseconds(100),
            CancellationToken.None);

        Assert.False(wait.IsCompleted);

        await wait.WaitAsync(TimeSpan.FromSeconds(1));
    }
}
