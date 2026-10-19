namespace Nordiska.Reporting.Worker.TaxReports;

public sealed class TaxReportWorkerOptions
{
    public int Concurrency { get; set; } = 12;

    public int PollIntervalSeconds { get; set; } = 30;

    public int LeaseDurationMinutes { get; set; } = 5;

    public int RetryDelaySeconds { get; set; } = 30;

    public int MaximumAttempts { get; set; } = 3;
}
