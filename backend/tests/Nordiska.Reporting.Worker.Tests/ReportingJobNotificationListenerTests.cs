using Microsoft.Extensions.Logging.Abstractions;

namespace Nordiska.Reporting.Worker.Tests;

public sealed class ReportingJobNotificationListenerTests
{
    [Fact]
    public async Task ExecuteAsync_WakesWorkersWhenReadyAndWhenNotified()
    {
        var source = new ControlledNotificationSource();
        var signal = new ReportingJobWakeSignal(workerCount: 1);
        var listener = new ReportingJobNotificationListener(
            source,
            signal,
            NullLogger<ReportingJobNotificationListener>.Instance);

        await listener.StartAsync(CancellationToken.None);
        await source.Ready.Task.WaitAsync(TimeSpan.FromSeconds(1));

        await signal.WaitAsync(
            Timeout.InfiniteTimeSpan,
            CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(1));

        Task notificationWake = signal.WaitAsync(
            Timeout.InfiniteTimeSpan,
            CancellationToken.None);

        source.Notify();

        await notificationWake.WaitAsync(TimeSpan.FromSeconds(1));
        await listener.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_WhenListeningFails_Reconnects()
    {
        var source = new FailOnceNotificationSource();
        var listener = new ReportingJobNotificationListener(
            source,
            new ReportingJobWakeSignal(workerCount: 1),
            NullLogger<ReportingJobNotificationListener>.Instance);

        await listener.StartAsync(CancellationToken.None);

        await source.SecondAttempt.Task
            .WaitAsync(TimeSpan.FromSeconds(3));

        await listener.StopAsync(CancellationToken.None);
    }

    private sealed class ControlledNotificationSource
        : IReportingJobNotificationSource
    {
        private Action? _notificationReceived;

        public TaskCompletionSource Ready { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task ListenAsync(
            Action listenerReady,
            Action notificationReceived,
            CancellationToken cancellationToken)
        {
            _notificationReceived = notificationReceived;
            listenerReady();
            Ready.TrySetResult();

            await Task.Delay(
                Timeout.InfiniteTimeSpan,
                cancellationToken);
        }

        public void Notify() =>
            _notificationReceived?.Invoke();
    }

    private sealed class FailOnceNotificationSource
        : IReportingJobNotificationSource
    {
        private int _attempts;

        public TaskCompletionSource SecondAttempt { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task ListenAsync(
            Action listenerReady,
            Action notificationReceived,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _attempts) == 1)
                throw new InvalidOperationException("Connection lost.");

            SecondAttempt.TrySetResult();

            await Task.Delay(
                Timeout.InfiniteTimeSpan,
                cancellationToken);
        }
    }
}
