namespace Nordiska.Reporting.Worker;

public sealed class JobNotificationSignal
{
    private readonly SemaphoreSlim _signal = new(0, 1);

    public void Notify()
    {
        if (_signal.CurrentCount == 0)
        {
            _signal.Release();
        }
    }

    public async Task<bool> WaitAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        return await _signal.WaitAsync(timeout, cancellationToken);
    }
}
