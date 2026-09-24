namespace Nordiska.Reporting.Worker;

/// <summary>
/// Background worker that runs periodic tasks for the reporting worker process.
/// </summary>
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly JobNotificationSignal _signal;
    private readonly IServiceScopeFactory _scopeFactory;

    public Worker(
        ILogger<Worker> logger,
        JobNotificationSignal signal,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _signal = signal;
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Runs the worker loop until cancellation is requested.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var notified = await _signal.WaitAsync(
                TimeSpan.FromSeconds(2),
                stoppingToken);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    notified
                        ? "Worker woke up because a tax report job notification was received."
                        : "Worker polling for tax report jobs at {time}.",
                    DateTimeOffset.Now);
            }

            await ProcessAvailableJobsAsync(stoppingToken);
        }
    }

    private async Task ProcessAvailableJobsAsync(CancellationToken stoppingToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<TaxReportJobProcessor>();

        var requeued = await processor.RequeueStaleAsync(
            TimeSpan.FromMinutes(15),
            stoppingToken);
        if (requeued > 0)
        {
            _logger.LogWarning("Requeued {Count} stale tax report jobs.", requeued);
        }

        while (await processor.ProcessNextAsync(stoppingToken))
        {
        }
    }
}
