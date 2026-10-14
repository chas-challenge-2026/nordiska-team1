using Microsoft.Extensions.Options;
using Nordiska.Reporting.Worker.AccountStatements;
using Nordiska.Reporting.Worker.TaxReports;

namespace Nordiska.Reporting.Worker;

public sealed class Worker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TaxReportWorkerOptions _options;
    private readonly ILogger<Worker> _logger;

    private readonly string _workerId =
        $"{Environment.MachineName}-" +
        $"{Guid.NewGuid():N}";

    public Worker(
        IServiceScopeFactory scopeFactory,
        IOptions<TaxReportWorkerOptions> options,
        ILogger<Worker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Reporting worker {WorkerId} started.",
            _workerId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using IServiceScope scope =
                    _scopeFactory.CreateScope();

                TaxReportJobProcessor processor =
                    scope.ServiceProvider
                        .GetRequiredService<
                            TaxReportJobProcessor>();

                AccountStatementJobProcessor accountStatementProcessor =
                    scope.ServiceProvider
                        .GetRequiredService<
                            AccountStatementJobProcessor>();

                bool taxReportProcessed =
                    await processor.ProcessNextAsync(
                        _workerId,
                        stoppingToken);

                bool accountStatementProcessed =
                    await accountStatementProcessor.ProcessNextAsync(
                        _workerId,
                        stoppingToken);

                if (!taxReportProcessed &&
                    !accountStatementProcessed)
                {
                    await Task.Delay(
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
                    "Reporting worker loop failed.");

                await Task.Delay(
                    TimeSpan.FromSeconds(
                        _options.PollIntervalSeconds),
                    stoppingToken);
            }
        }
    }
}
