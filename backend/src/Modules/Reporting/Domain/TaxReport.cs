namespace Bank.Modules.Report.Domain;

public sealed class TaxReport
{
    public long Id { get; set; }
    public long AccountId { get; set; }
    public int Year { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? DownloadUrl { get; set; }
    public string? Signature { get; set; }
    public DateTime CreatedAt { get; set; }
}
