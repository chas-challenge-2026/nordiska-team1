using Microsoft.Extensions.Options;

namespace Nordiska.Reporting.Worker.ResourceManagement;

internal sealed class ResourceMonitor : BackgroundService
{
    private readonly IProcessResourceReader _reader;
    private readonly ResourceManagementOptions _options;
    private readonly ILogger<ResourceMonitor> _logger;

    public ResourceMonitor(
        IProcessResourceReader reader,
        IOptions<ResourceManagementOptions> options,
        ILogger<ResourceMonitor> logger)
    {
        _reader = reader;
        _options = options.Value;
        _logger = logger;

        if (_options.SampleIntervalSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(_options.SampleIntervalSeconds),
                "Resource sample interval must be greater than zero.");
        }
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        ProcessResourceSnapshot? previous = null;
        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(_options.SampleIntervalSeconds));

        try
        {
            do
            {
                try
                {
                    previous = CaptureAndLog(previous);
                }
                catch (Exception exception)
                    when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogWarning(
                        exception,
                        "Worker resource sample failed; job processing continues.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        { }
    }

    internal ProcessResourceSnapshot CaptureAndLog(
        ProcessResourceSnapshot? previous)
    {
        ProcessResourceSnapshot current = _reader.Read();
        bool cpuSampleReady = previous is not null;
        double cpuPercent = cpuSampleReady
            ? ProcessResourceSnapshot.CalculateCpuPercent(
                previous!,
                current,
                Environment.ProcessorCount)
            : 0;

        _logger.LogInformation(
            "Worker resources CpuPercent={CpuPercent:F1} CpuSampleReady={CpuSampleReady} WorkingSetBytes={WorkingSetBytes} ManagedHeapBytes={ManagedHeapBytes} ThreadPoolThreads={ThreadPoolThreads} PendingThreadPoolWorkItems={PendingThreadPoolWorkItems} Gen0Collections={Gen0Collections} Gen1Collections={Gen1Collections} Gen2Collections={Gen2Collections}",
            cpuPercent,
            cpuSampleReady,
            current.WorkingSetBytes,
            current.ManagedHeapBytes,
            current.ThreadPoolThreads,
            current.PendingThreadPoolWorkItems,
            current.Gen0Collections,
            current.Gen1Collections,
            current.Gen2Collections);

        return current;
    }
}
