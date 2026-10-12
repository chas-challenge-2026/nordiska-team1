using Microsoft.EntityFrameworkCore;
using Nordiska.Modules.Reporting.Application;
using Nordiska.Modules.Reporting.Domain;
using Nordiska.Modules.Reporting.Infrastructure.Db;

namespace Nordiska.Modules.Reporting.Infrastructure;

public sealed class AnnualTaxReportQueryService
    : IAnnualTaxReportQueryService
{
    private readonly ReportingDbContext _dbContext;
    private readonly IReportDocumentStorage _storage;

    public AnnualTaxReportQueryService(
        ReportingDbContext dbContext,
        IReportDocumentStorage storage)
    {
        _dbContext = dbContext;
        _storage = storage;
    }

    public Task<AnnualTaxReportJobStatus?> GetStatusAsync(
        long customerId,
        long jobId,
        CancellationToken cancellationToken)
    {
        return (
            from job in _dbContext.TaxReportJobs.AsNoTracking()
            join report in _dbContext.TaxReports.AsNoTracking()
                on job.TaxReportId equals report.Id
            where job.Id == jobId &&
                  report.CustomerId == customerId
            select new AnnualTaxReportJobStatus(
                job.Id,
                report.Id,
                job.Status,
                job.CreatedAt,
                job.CompletedAt,
                job.Status == TaxReportJobStatuses.Failed
                    ? "PDF generation failed."
                    : null))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<AnnualTaxReportDownload?> DownloadAsync(
        long customerId,
        long taxReportId,
        CancellationToken cancellationToken)
    {
        DocumentMetadata? document =
            await (
                from report in _dbContext.TaxReports.AsNoTracking()
                join link in _dbContext.TaxReportDocuments.AsNoTracking()
                    on report.Id equals link.TaxReportId
                join generated in _dbContext.GeneratedDocuments.AsNoTracking()
                    on link.DocumentId equals generated.Id
                where report.Id == taxReportId &&
                      report.CustomerId == customerId
                orderby link.CreatedAt descending
                select new DocumentMetadata(
                    generated.FileName,
                    generated.ContentType,
                    generated.StorageKey,
                    generated.Sha256Hash))
            .FirstOrDefaultAsync(cancellationToken);

        if (document is null)
        {
            return null;
        }

        byte[] content = await _storage.ReadAsync(
            document.StorageKey,
            document.Sha256Hash,
            cancellationToken);

        return new AnnualTaxReportDownload(
            document.FileName,
            document.ContentType,
            content);
    }

    private sealed record DocumentMetadata(
        string FileName,
        string ContentType,
        string StorageKey,
        string Sha256Hash);
}
