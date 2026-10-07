using Nordiska.Modules.Faq.Application;
using Nordiska.Modules.Faq.Domain;

namespace Nordiska.FrontendApi.BackgroundWorkers;

/// <summary>
/// Saves queued FAQ searches to the database in batches.
/// </summary>
public sealed class FaqSearchLogBackgroundWorker : BackgroundService
{
    private const int MaxBatchSize = 100;

    private readonly FaqSearchLogQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FaqSearchLogBackgroundWorker> _logger;

    public FaqSearchLogBackgroundWorker(
        FaqSearchLogQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<FaqSearchLogBackgroundWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<FaqSearchLog>(MaxBatchSize);

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

    // A failed batch is logged and dropped, losing a few search logs is better than stopping the worker
    private async Task SaveBatchAsync(List<FaqSearchLog> batch, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IFaqSearchLogRepository>();
            await repository.AddRangeAsync(batch, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save {Count} FAQ search log(s)", batch.Count);
        }
    }
}
