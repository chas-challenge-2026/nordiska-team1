using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nordiska.Modules.Banking.Application;

namespace Nordiska.FrontendApi.BackgroundWorkers;

/// <summary>
/// Background worker that periodically polls and executes pending planned and recurring transactions.
/// </summary>
public sealed class PlannedTransactionsBackgroundWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PlannedTransactionsBackgroundWorker> _logger;
    private readonly TimeSpan _checkInterval;

    public PlannedTransactionsBackgroundWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<PlannedTransactionsBackgroundWorker> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        var intervalSeconds = configuration.GetValue<int?>("Banking:PlannedWorkerIntervalSeconds") ?? 60;
        _checkInterval = TimeSpan.FromSeconds(Math.Max(1, intervalSeconds));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PlannedTransactionsBackgroundWorker started with interval {Interval}.", _checkInterval);

        using var timer = new PeriodicTimer(_checkInterval);

        // Run an initial check on startup
        try
        {
            await Task.Delay(1000, stoppingToken);
            await ProcessPendingTransactionsAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during initial planned transactions execution pass.");
        }

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ProcessPendingTransactionsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred during planned transactions background processing.");
            }
        }

        _logger.LogInformation("PlannedTransactionsBackgroundWorker stopped.");
    }

    public async Task<int> ProcessPendingTransactionsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var txRepo = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
        var txService = scope.ServiceProvider.GetRequiredService<ITransactionService>();

        var pending = await txRepo.GetPendingPlannedTransactionsAsync(DateTime.UtcNow, cancellationToken);
        if (pending.Count == 0)
        {
            return 0;
        }

        _logger.LogInformation("Found {Count} pending planned transaction(s) to process.", pending.Count);
        var processedCount = 0;

        foreach (var plan in pending)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                var result = await txService.ProcessPlannedTransactionAsync(plan.Id, cancellationToken);
                if (result != null)
                {
                    processedCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process planned transaction ID {PlanId}.", plan.Id);
            }
        }

        _logger.LogInformation("Finished planned transactions pass. Successfully executed {ProcessedCount} of {TotalCount}.", processedCount, pending.Count);
        return processedCount;
    }
}
