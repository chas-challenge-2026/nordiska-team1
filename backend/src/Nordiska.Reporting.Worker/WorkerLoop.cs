using Microsoft.Extensions.Options;
using Nordiska.Reporting.Worker.AccountStatements;
using Nordiska.Reporting.Worker.TaxReports;

namespace Nordiska.Reporting.Worker;

internal interface IWorkerLoop
{
    Task RunAsync(
        string workerId,
        CancellationToken stoppingToken);
}

internal sealed class WorkerLoop : IWorkerLoop
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IReportingJobWakeSignal _wakeSignal;
    private readonly TaxReportWorkerOptions _options;
    private readonly ILogger<WorkerLoop> _logger;

    public WorkerLoop(
        IServiceScopeFactory scopeFactory,
        IReportingJobWakeSignal wakeSignal,
        IOptions<TaxReportWorkerOptions> options,
        ILogger<WorkerLoop> logger)
    {
        _scopeFactory = scopeFactory;
        _wakeSignal = wakeSignal;
        _options = options.Value;
        _logger = logger;
    }

    public async Task RunAsync(
        string workerId,
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Reporting worker slot {WorkerId} started.",
            workerId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using AsyncServiceScope scope =
                    _scopeFactory.CreateAsyncScope();

                TaxReportJobProcessor taxReportProcessor =
                    scope.ServiceProvider
                        .GetRequiredService<TaxReportJobProcessor>();

                AccountStatementJobProcessor accountStatementProcessor =
                    scope.ServiceProvider
                        .GetRequiredService<AccountStatementJobProcessor>();

                bool taxReportProcessed =
                    await taxReportProcessor.ProcessNextAsync(
                        workerId,
                        stoppingToken);

                bool accountStatementProcessed =
                    await accountStatementProcessor.ProcessNextAsync(
                        workerId,
                        stoppingToken);

                if (!taxReportProcessed &&
                    !accountStatementProcessed)
                {
                    await _wakeSignal.WaitAsync(
                        TimeSpan.FromSeconds(
                            _options.PollIntervalSeconds),
                        stoppingToken);
                }
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

                try
                {
                    await _wakeSignal.WaitAsync(
                        TimeSpan.FromSeconds(
                            _options.PollIntervalSeconds),
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
}
