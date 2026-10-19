using Nordiska.Reporting.Worker.Lifecycle;
using Nordiska.Reporting.Worker.Scheduling;

namespace Nordiska.Reporting.Worker.Execution;

internal interface IWorkerLoop
{
    Task RunAsync(
        string workerId,
        CancellationToken stoppingToken);
}

internal sealed class WorkerLoop : IWorkerLoop
{
    private readonly IReportingJobScheduler _scheduler;
    private readonly WorkerLifecycle _lifecycle;
    private readonly ILogger<WorkerLoop> _logger;

    public WorkerLoop(
        IReportingJobScheduler scheduler,
        WorkerLifecycle lifecycle,
        ILogger<WorkerLoop> logger)
    {
        _scheduler = scheduler;
        _lifecycle = lifecycle;
        _logger = logger;
    }

    public async Task RunAsync(
        string workerId,
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Reporting worker slot {WorkerId} started.",
            workerId);

        while (!stoppingToken.IsCancellationRequested &&
               _lifecycle.IsAcceptingWork)
        {
            try
            {
                await _scheduler.RunCycleAsync(workerId, stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Reporting worker slot {WorkerId} failed.",
                    workerId);

                if (!_lifecycle.IsAcceptingWork)
                    break;

                try
                {
                    await _scheduler.WaitForWorkAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        _logger.LogInformation(
            "Reporting worker slot {WorkerId} stopped.",
            workerId);
    }
}
