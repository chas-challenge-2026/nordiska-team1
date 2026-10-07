using Microsoft.Extensions.Options;
using Nordiska.Modules.Faq.Application;

namespace Nordiska.FrontendApi.BackgroundWorkers;

/// <summary>
/// Deletes FAQ search logs older than the retention period, once at startup and then on an interval.
/// </summary>
public sealed class FaqSearchLogCleanupWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly FaqSearchLogOptions _options;
    private readonly ILogger<FaqSearchLogCleanupWorker> _logger;

    public FaqSearchLogCleanupWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<FaqSearchLogOptions> options,
        ILogger<FaqSearchLogCleanupWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(_options.CleanupIntervalHours));

        try
        {
            // Small delay so the cleanup doesn't compete with migrations and seeding at startup
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            do
            {
                await CleanupAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IFaqSearchLogRepository>();

            var cutoff = DateTime.UtcNow.AddDays(-_options.RetentionDays);
            var deleted = await repository.DeleteOlderThanAsync(cutoff, cancellationToken);

            if (deleted > 0)
            {
                _logger.LogInformation("Deleted {Count} FAQ search log(s) older than {Cutoff}", deleted, cutoff);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clean up old FAQ search logs");
        }
    }
}
