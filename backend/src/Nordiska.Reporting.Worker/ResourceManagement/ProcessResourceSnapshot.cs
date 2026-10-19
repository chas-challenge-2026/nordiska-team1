namespace Nordiska.Reporting.Worker.ResourceManagement;

internal sealed record ProcessResourceSnapshot(
    DateTimeOffset SampledAt,
    TimeSpan ProcessCpuTime,
    long WorkingSetBytes,
    long ManagedHeapBytes,
    int ThreadPoolThreads,
    long PendingThreadPoolWorkItems,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections)
{
    public static double CalculateCpuPercent(
        ProcessResourceSnapshot previous,
        ProcessResourceSnapshot current,
        int processorCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(processorCount);

        double elapsedSeconds =
            (current.SampledAt - previous.SampledAt).TotalSeconds;
        double cpuSeconds =
            (current.ProcessCpuTime - previous.ProcessCpuTime)
            .TotalSeconds;

        if (elapsedSeconds <= 0 || cpuSeconds <= 0)
            return 0;

        return Math.Clamp(
            cpuSeconds / (elapsedSeconds * processorCount) * 100,
            0,
            100);
    }
}
