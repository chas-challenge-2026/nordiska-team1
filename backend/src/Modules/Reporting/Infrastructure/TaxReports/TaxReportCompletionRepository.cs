using Microsoft.EntityFrameworkCore;
using Nordiska.Modules.Reporting.Application;
using Nordiska.Modules.Reporting.Domain;
using Nordiska.Modules.Reporting.Infrastructure.Db;

namespace Nordiska.Modules.Reporting.Infrastructure;

public sealed class TaxReportCompletionRepository
    : ITaxReportCompletionRepository
{
    private readonly ReportingDbContext _dbContext;

    public TaxReportCompletionRepository(
        ReportingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task CompleteAsync(
        long jobId,
        string workerId,
        CompletedReportDocument completedDocument,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        TaxReportJob job =
            await _dbContext.TaxReportJobs.SingleAsync(
                candidate =>
                    candidate.Id == jobId &&
                    candidate.TaxReportId ==
                        completedDocument.TaxReportId &&
                    candidate.Status ==
                        TaxReportJobStatuses.Processing &&
                    candidate.LockedBy == workerId,
                cancellationToken);

        GeneratedDocument? document =
            await _dbContext.GeneratedDocuments
                .SingleOrDefaultAsync(
                    candidate =>
                        candidate.StorageKey ==
                            completedDocument.StorageKey,
                    cancellationToken);

        if (document is null)
        {
            document =
                GeneratedDocument.Create(
                    documentType: "AnnualTaxReport",
                    contentType: "application/pdf",
                    fileName: completedDocument.FileName,
                    storageKey:
                        completedDocument.StorageKey,
                    byteLength:
                        completedDocument.ByteLength,
                    sha256Hash:
                        completedDocument.Sha256Hash,
                    createdAt: completedAt);

            _dbContext.GeneratedDocuments.Add(document);

            await _dbContext.SaveChangesAsync(
                cancellationToken);
        }
        else if (!string.Equals(
                     document.Sha256Hash,
                     completedDocument.Sha256Hash,
                     StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Existing document metadata has a different hash.");
        }

        bool relationExists =
            await _dbContext.TaxReportDocuments.AnyAsync(
                link =>
                    link.TaxReportId ==
                        completedDocument.TaxReportId &&
                    link.DocumentId == document.Id,
                cancellationToken);

        if (!relationExists)
        {
            _dbContext.TaxReportDocuments.Add(
                TaxReportDocument.Create(
                    completedDocument.TaxReportId,
                    document.Id,
                    completedAt));
        }

        job.MarkCompleted(
            workerId,
            completedAt);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);
    }
}