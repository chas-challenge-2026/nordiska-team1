namespace Nordiska.Modules.Reporting.Domain;

public sealed class TaxReportJob
{
    private TaxReportJob()
    {}

    private TaxReportJob(
        long taxReportId,
        string status,
        DateTimeOffset createdAt)
    {
        TaxReportId = taxReportId;
        Status = status;
        CreatedAt = createdAt;
    }

    public long Id { get; private set; }

    public long TaxReportId { get; private set; }

    public string Status { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    public static TaxReportJob Create(
        long taxReportId,
        DateTimeOffset createdAt)
    {
        return new TaxReportJob(
            taxReportId,
            "Pending",
            createdAt);
    }

    public void MarkCompleted()
    {
        Status = "Completed";
    }

    public void MarkFailed()
    {
        Status = "Failed";
    }




}