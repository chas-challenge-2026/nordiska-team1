using System.Diagnostics;

namespace Nordiska.Reporting.Worker.ResourceManagement;

internal interface IProcessResourceReader
{
    ProcessResourceSnapshot Read();
}

internal sealed class ProcessResourceReader : IProcessResourceReader
{
    public ProcessResourceSnapshot Read()
    {
        using Process process = Process.GetCurrentProcess();
        process.Refresh();

        return new ProcessResourceSnapshot(
            DateTimeOffset.UtcNow,
            process.TotalProcessorTime,
            process.WorkingSet64,
            GC.GetTotalMemory(forceFullCollection: false),
            ThreadPool.ThreadCount,
            ThreadPool.PendingWorkItemCount,
            GC.CollectionCount(0),
            GC.CollectionCount(1),
            GC.CollectionCount(2));
    }
}
