using Microsoft.Extensions.Options;
using Nordiska.Reporting.Worker.TaxReports;

namespace Nordiska.Reporting.Worker;

internal sealed class Worker : BackgroundService
{
    private readonly IWorkerLoop _workerLoop;
    private readonly TaxReportWorkerOptions _options;
    private readonly ILogger<Worker> _logger;

    private readonly string _workerInstanceId =
        $"{Environment.MachineName}-" +
        $"{Guid.NewGuid():N}";

    public Worker(
        IWorkerLoop workerLoop,
        IOptions<TaxReportWorkerOptions> options,
        ILogger<Worker> logger)
    {
        _workerLoop = workerLoop;
        _options = options.Value;
        _logger = logger;

        if (_options.Concurrency <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(_options.Concurrency),
                "Worker concurrency must be greater than zero.");
        }
    }

    protected override Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Reporting worker pool {WorkerInstanceId} started with {Concurrency} slots.",
            _workerInstanceId,
            _options.Concurrency);

        var workers = new Task[_options.Concurrency];

        for (var index = 0; index < workers.Length; index++)
        {
            string workerId =
                $"{_workerInstanceId}-{index + 1:D2}";
            workers[index] = _workerLoop.RunAsync(
                workerId,
                stoppingToken);
        }

        return Task.WhenAll(workers);
    }
}
