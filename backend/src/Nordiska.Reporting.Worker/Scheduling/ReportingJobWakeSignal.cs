using System.Threading.Channels;

namespace Nordiska.Reporting.Worker.Scheduling;

internal interface IReportingJobWakeSignal
{
    void Pulse();

    Task WaitAsync(
        TimeSpan fallbackDelay,
        CancellationToken cancellationToken);
}

internal sealed class ReportingJobWakeSignal
    : IReportingJobWakeSignal
{
    private readonly int _workerCount;
    private readonly Channel<bool> _signals;

    public ReportingJobWakeSignal(int workerCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            workerCount);

        _workerCount = workerCount;
        _signals = Channel.CreateBounded<bool>(
            new BoundedChannelOptions(workerCount)
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = false,
                SingleWriter = false
            });
    }

    public void Pulse()
    {
        for (var index = 0; index < _workerCount; index++)
            _signals.Writer.TryWrite(true);
    }

    public async Task WaitAsync(
        TimeSpan fallbackDelay,
        CancellationToken cancellationToken)
    {
        using var fallbackCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

        fallbackCancellation.CancelAfter(fallbackDelay);

        try
        {
            await _signals.Reader.ReadAsync(
                fallbackCancellation.Token);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        { }
    }
}
