namespace Nordiska.Modules.Reporting.Domain;

public sealed class TaxReportJob
{
    public Guid Id { get; set; }

    public long CustomerId { get; set; }

    public long AccountId { get; set; }

    public int Year { get; set; }

    public string Status { get; set; } = "Pending";

    public string? DownloadUrl { get; set; }

    public int? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}