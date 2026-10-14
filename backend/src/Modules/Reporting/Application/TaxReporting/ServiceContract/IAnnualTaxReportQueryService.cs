namespace Nordiska.Modules.Reporting.Application;

public sealed record AnnualTaxReportJobStatus(
    long JobId,
    long TaxReportId,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    string? Error);

public sealed record AnnualTaxReportDownload(
    string FileName,
    string ContentType,
    byte[] Content);

public interface IAnnualTaxReportQueryService
{
    Task<AnnualTaxReportJobStatus?> GetStatusAsync(
        long customerId,
        long jobId,
        CancellationToken cancellationToken);

    Task<AnnualTaxReportDownload?> DownloadAsync(
        long customerId,
        long taxReportId,
        CancellationToken cancellationToken);
}
