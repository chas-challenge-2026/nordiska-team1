namespace Nordiska.Reporting.Worker.Scheduling;

public sealed class TaxReportWorkerOptions
{
    public int Concurrency { get; set; } = 12; // slots 

    public int PollIntervalSeconds { get; set; } = 30;

    public int LeaseDurationMinutes { get; set; } = 5;

    public int RetryDelaySeconds { get; set; } = 30;

    public int MaximumAttempts { get; set; } = 3;

    public int DrainTimeoutSeconds { get; set; } = 25;
}
