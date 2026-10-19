using Microsoft.Extensions.Options;
using Nordiska.Reporting.Worker.Execution;
using Nordiska.Reporting.Worker.Lifecycle;

namespace Nordiska.Reporting.Worker.Scheduling;

internal interface IReportingJobScheduler
{
    Task RunCycleAsync(
        string workerId,
        CancellationToken cancellationToken);

    Task WaitForWorkAsync(CancellationToken cancellationToken);
}

internal sealed class ReportingJobScheduler : IReportingJobScheduler
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IReportingJobWakeSignal _wakeSignal;
    private readonly WorkerLifecycle _lifecycle;
    private readonly TaxReportWorkerOptions _options;

    public ReportingJobScheduler(
        IServiceScopeFactory scopeFactory,
        IReportingJobWakeSignal wakeSignal,
        WorkerLifecycle lifecycle,
        IOptions<TaxReportWorkerOptions> options)
    {
        _scopeFactory = scopeFactory;
        _wakeSignal = wakeSignal;
        _lifecycle = lifecycle;
        _options = options.Value;
    }

    public async Task RunCycleAsync(
        string workerId,
        CancellationToken cancellationToken)
    {
        if (!_lifecycle.IsAcceptingWork)
            return;

        bool processed = await ProcessAvailableJobsAsync(
            workerId,
            cancellationToken);

        if (!processed && _lifecycle.IsAcceptingWork)
            await WaitForWorkAsync(cancellationToken);
    }

    public Task WaitForWorkAsync(CancellationToken cancellationToken) =>
        _wakeSignal.WaitAsync(TimeSpan.FromSeconds(_options.PollIntervalSeconds),cancellationToken);

    private async Task<bool> ProcessAvailableJobsAsync(string workerId,CancellationToken cancellationToken)
    {
        if (!_lifecycle.IsAcceptingWork)
            return false;

        await using AsyncServiceScope scope =
            _scopeFactory.CreateAsyncScope();

        bool taxReportProcessed = await ProcessTaxReportAsync(
            scope.ServiceProvider,
            workerId,
            cancellationToken);

        if (!_lifecycle.IsAcceptingWork)
            return taxReportProcessed;

        bool accountStatementProcessed =
            await ProcessAccountStatementAsync(
                scope.ServiceProvider,
                workerId,
                cancellationToken);

        return taxReportProcessed || accountStatementProcessed;
    }

    private static Task<bool> ProcessTaxReportAsync(
        IServiceProvider services,
        string workerId,
        CancellationToken cancellationToken) =>
        services.GetRequiredService<TaxReportJobProcessor>()
            .ProcessNextAsync(workerId, cancellationToken);

    private static Task<bool> ProcessAccountStatementAsync(
        IServiceProvider services,
        string workerId,
        CancellationToken cancellationToken) =>
        services.GetRequiredService<AccountStatementJobProcessor>()
            .ProcessNextAsync(workerId, cancellationToken);
}
