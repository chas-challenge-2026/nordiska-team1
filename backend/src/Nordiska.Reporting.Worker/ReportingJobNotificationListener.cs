namespace Nordiska.Reporting.Worker;

internal interface IReportingJobNotificationSource
{
    Task ListenAsync(
        Action listenerReady,
        Action notificationReceived,
        CancellationToken cancellationToken);
}

internal sealed class ReportingJobNotificationListener
    : BackgroundService
{
    private readonly IReportingJobNotificationSource _source;
    private readonly IReportingJobWakeSignal _wakeSignal;
    private readonly ILogger<ReportingJobNotificationListener> _logger;

    public ReportingJobNotificationListener(
        IReportingJobNotificationSource source,
        IReportingJobWakeSignal wakeSignal,
        ILogger<ReportingJobNotificationListener> logger)
    {
        _source = source;
        _wakeSignal = wakeSignal;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _source.ListenAsync(
                    _wakeSignal.Pulse,
                    _wakeSignal.Pulse,
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Reporting job notification listener disconnected. Reconnecting.");
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(2),
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
