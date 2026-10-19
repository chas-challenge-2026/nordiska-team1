using Microsoft.Extensions.Options;
using Nordiska.Reporting.Worker.Execution;
using Nordiska.Reporting.Worker.Scheduling;

namespace Nordiska.Reporting.Worker.Lifecycle;

internal sealed class Worker : BackgroundService
{
    private readonly IWorkerLoop _workerLoop;
    private readonly WorkerLifecycle _lifecycle;
    private readonly IReportingJobWakeSignal _wakeSignal;
    private readonly TaxReportWorkerOptions _options;
    private readonly ILogger<Worker> _logger;

    private readonly string _workerInstanceId =
        $"{Environment.MachineName}-" +
        $"{Guid.NewGuid():N}";

    public Worker(
        IWorkerLoop workerLoop,
        WorkerLifecycle lifecycle,
        IReportingJobWakeSignal wakeSignal,
        IOptions<TaxReportWorkerOptions> options,
        ILogger<Worker> logger)
    {
        _workerLoop = workerLoop;
        _lifecycle = lifecycle;
        _wakeSignal = wakeSignal;
        _options = options.Value;
        _logger = logger;

        if (_options.Concurrency <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(_options.Concurrency),
                "Worker concurrency must be greater than zero.");
        }

        if (_options.DrainTimeoutSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(_options.DrainTimeoutSeconds),
                "Worker drain timeout must be greater than zero.");
        }
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _lifecycle.Start();
        _logger.LogInformation(
            "Reporting worker pool {WorkerInstanceId} started with {Concurrency} slots.",
            _workerInstanceId,
            _options.Concurrency);

        try
        {
            /*
             * Creates one Task per worker slot (_options.Concurrency)!
             * So if concurrency is 3, the array has three positions. Each position
             * will hold the running Task for one independent worker loop.
             * 
             */
            var workers = new Task[_options.Concurrency]; 
            // 
            for (var index = 0; index < workers.Length; index++)
            {
                string workerId =$"{_workerInstanceId}-{index + 1:D2}"; //worker ids
                workers[index] = _workerLoop.RunAsync(
                    workerId,
                    stoppingToken);
            }

            await Task.WhenAll(workers);
        }
        finally
        {
            _lifecycle.MarkStopped();
            _logger.LogInformation(
                "Reporting worker pool {WorkerInstanceId} stopped.",
                _workerInstanceId);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_lifecycle.BeginDraining())
        {
            _logger.LogInformation(
                "Reporting worker pool {WorkerInstanceId} is draining.",
                _workerInstanceId);
            _wakeSignal.Pulse();
        }

        using var drainCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        drainCancellation.CancelAfter(
            TimeSpan.FromSeconds(_options.DrainTimeoutSeconds));

        try
        {
            if (ExecuteTask is { } running)
                await running.WaitAsync(drainCancellation.Token);
        }
        catch (OperationCanceledException)
            when (drainCancellation.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Reporting worker pool {WorkerInstanceId} exceeded its drain timeout; active work will be interrupted and can be reclaimed after its lease expires.",
                _workerInstanceId);
        }
        finally
        {
            await base.StopAsync(drainCancellation.Token);
        }
    }
}
