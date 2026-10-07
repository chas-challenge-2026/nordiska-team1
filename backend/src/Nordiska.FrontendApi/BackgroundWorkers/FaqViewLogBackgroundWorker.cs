using Nordiska.Modules.Faq.Application;
using Nordiska.Modules.Faq.Domain;

namespace Nordiska.FrontendApi.BackgroundWorkers;

/// <summary>
/// Saves queued FAQ article views to the database in batches.
/// </summary>
public sealed class FaqViewLogBackgroundWorker : BackgroundService
{
    private const int MaxBatchSize = 100;

    private readonly FaqViewLogQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FaqViewLogBackgroundWorker> _logger;

    public FaqViewLogBackgroundWorker(
        FaqViewLogQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<FaqViewLogBackgroundWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<FaqViewLog>(MaxBatchSize);

        try
        {
            while (await _queue.Reader.WaitToReadAsync(stoppingToken))
            {
                while (batch.Count < MaxBatchSize && _queue.Reader.TryRead(out var log))
                {
                    batch.Add(log);
                }

                await SaveBatchAsync(batch, stoppingToken);
                batch.Clear();
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    // Same as the search log, a failed batch is logged and dropped instead of stopping the worker
    private async Task SaveBatchAsync(List<FaqViewLog> batch, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IFaqViewLogRepository>();
            await repository.AddRangeAsync(batch, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save {Count} FAQ view log(s)", batch.Count);
        }
    }
}
