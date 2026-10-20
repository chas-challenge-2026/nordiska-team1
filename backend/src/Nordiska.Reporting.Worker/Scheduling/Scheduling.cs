using System.Collections.Concurrent;
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
    private static readonly TimeSpan EmptyQueueReprobeInterval =
        TimeSpan.FromSeconds(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IReportingJobWakeSignal _wakeSignal;
    private readonly WorkerLifecycle _lifecycle;
    private readonly TimeProvider _timeProvider;
    private readonly TaxReportWorkerOptions _options;
    // Each workerid is used by one slot loop.
    private readonly ConcurrentDictionary<string, SlotProbeState> _slotStates = new();

    public ReportingJobScheduler(
        IServiceScopeFactory scopeFactory,
        IReportingJobWakeSignal wakeSignal,
        WorkerLifecycle lifecycle,
        TimeProvider timeProvider,
        IOptions<TaxReportWorkerOptions> options)
    {
        _scopeFactory = scopeFactory;
        _wakeSignal = wakeSignal;
        _lifecycle = lifecycle;
        _timeProvider = timeProvider;
        _options = options.Value;
    }

    public async Task RunCycleAsync(
        string workerId,
        CancellationToken cancellationToken)
    {
        if (!_lifecycle.IsAcceptingWork)
            return;

        SlotProbeState slotState = _slotStates.GetOrAdd(
            workerId,
            static _ => new SlotProbeState());

        bool processed = await ProcessAvailableJobsAsync(
            workerId,
            slotState,
            cancellationToken);

        if (!processed && _lifecycle.IsAcceptingWork)
        {
            await WaitForWorkAsync(cancellationToken);
            slotState.Reset();
        }
    }

    public Task WaitForWorkAsync(CancellationToken cancellationToken) =>
        _wakeSignal.WaitAsync(TimeSpan.FromSeconds(_options.PollIntervalSeconds),cancellationToken);

    private async Task<bool> ProcessAvailableJobsAsync(
        string workerId,
        SlotProbeState slotState,
        CancellationToken cancellationToken)
    {
        if (!_lifecycle.IsAcceptingWork)
            return false;

        await using AsyncServiceScope scope =
            _scopeFactory.CreateAsyncScope();

        bool taxReportProcessed = false;
        if (slotState.ShouldProbeTax(_timeProvider.GetUtcNow()))
        {
            taxReportProcessed = await ProcessTaxReportAsync(
                scope.ServiceProvider,
                workerId,
                cancellationToken);
            slotState.RecordTaxProbe(
                taxReportProcessed,
                _timeProvider.GetUtcNow());
        }

        if (!_lifecycle.IsAcceptingWork)
            return taxReportProcessed;

        bool accountStatementProcessed = false;
        if (slotState.ShouldProbeAccount(_timeProvider.GetUtcNow()))
        {
            accountStatementProcessed = await ProcessAccountStatementAsync(
                scope.ServiceProvider,
                workerId,
                cancellationToken);
            slotState.RecordAccountProbe(
                accountStatementProcessed,
                _timeProvider.GetUtcNow());
        }

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

    private sealed class SlotProbeState
    {
        private DateTimeOffset _taxEmptyUntil;
        private DateTimeOffset _accountEmptyUntil;

        public bool ShouldProbeTax(DateTimeOffset now) =>
            now >= _taxEmptyUntil;

        public bool ShouldProbeAccount(DateTimeOffset now) =>
            now >= _accountEmptyUntil;

        public void RecordTaxProbe(bool processed, DateTimeOffset now) =>
            _taxEmptyUntil = processed
                ? DateTimeOffset.MinValue
                : now + EmptyQueueReprobeInterval;

        public void RecordAccountProbe(bool processed, DateTimeOffset now) =>
            _accountEmptyUntil = processed
                ? DateTimeOffset.MinValue
                : now + EmptyQueueReprobeInterval;

        public void Reset()
        {
            _taxEmptyUntil = DateTimeOffset.MinValue;
            _accountEmptyUntil = DateTimeOffset.MinValue;
        }
    }
}
