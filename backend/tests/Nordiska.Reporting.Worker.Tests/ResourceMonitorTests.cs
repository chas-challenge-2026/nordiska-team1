using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nordiska.Reporting.Worker.ResourceManagement;
using Xunit.Abstractions;

namespace Nordiska.Reporting.Worker.Tests;

public sealed class ResourceMonitorTests
{
    private readonly ITestOutputHelper _output;

    public ResourceMonitorTests(ITestOutputHelper output) =>
        _output = output;

    [Fact]
    public void CalculateCpuPercent_NormalizesAcrossProcessorCount()
    {
        ProcessResourceSnapshot before = Snapshot(
            DateTimeOffset.UnixEpoch,
            TimeSpan.FromSeconds(1));
        ProcessResourceSnapshot after = Snapshot(
            DateTimeOffset.UnixEpoch.AddSeconds(2),
            TimeSpan.FromSeconds(5));

        double percent = ProcessResourceSnapshot.CalculateCpuPercent(
            before,
            after,
            processorCount: 4);

        Assert.Equal(50, percent, precision: 3);
    }

    [Fact]
    public void CaptureAndLog_EmitsResourceValuesForTestAndBenchmarkLogs()
    {
        var reader = new SequenceReader(
            Snapshot(DateTimeOffset.UnixEpoch, TimeSpan.Zero),
            Snapshot(DateTimeOffset.UnixEpoch.AddSeconds(2),
                TimeSpan.FromSeconds(1)));
        var logger = new RecordingLogger<ResourceMonitor>(_output);
        var monitor = new ResourceMonitor(
            reader,
            Options.Create(new ResourceManagementOptions()),
            logger);

        ProcessResourceSnapshot first = monitor.CaptureAndLog(null);
        monitor.CaptureAndLog(first);

        Assert.Equal(2, logger.Messages.Count);
        Assert.Contains("CpuPercent", logger.Messages[1]);
        Assert.Contains("WorkingSetBytes", logger.Messages[1]);
        Assert.Contains("ManagedHeapBytes", logger.Messages[1]);
        Assert.Contains("ThreadPoolThreads", logger.Messages[1]);
    }

    private static ProcessResourceSnapshot Snapshot(
        DateTimeOffset sampledAt,
        TimeSpan cpuTime) =>
        new(
            sampledAt,
            cpuTime,
            WorkingSetBytes: 128 * 1024 * 1024,
            ManagedHeapBytes: 16 * 1024 * 1024,
            ThreadPoolThreads: 8,
            PendingThreadPoolWorkItems: 2,
            Gen0Collections: 1,
            Gen1Collections: 0,
            Gen2Collections: 0);

    private sealed class SequenceReader : IProcessResourceReader
    {
        private readonly Queue<ProcessResourceSnapshot> _samples;

        public SequenceReader(params ProcessResourceSnapshot[] samples) =>
            _samples = new Queue<ProcessResourceSnapshot>(samples);

        public ProcessResourceSnapshot Read() => _samples.Dequeue();
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        private readonly ITestOutputHelper _output;

        public RecordingLogger(ITestOutputHelper output) =>
            _output = output;

        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            string message = formatter(state, exception);
            Messages.Add(message);
            _output.WriteLine(message);
        }
    }
}
