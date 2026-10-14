using Microsoft.EntityFrameworkCore;
using Nordiska.Modules.Reporting.Application;
using Nordiska.Modules.Reporting.Domain;
using Nordiska.Modules.Reporting.Infrastructure.Db;

namespace Nordiska.Modules.Reporting.Infrastructure;

public sealed class AccountStatementCompletionRepository
    : IAccountStatementCompletionRepository
{
    private readonly ReportingDbContext _dbContext;

    public AccountStatementCompletionRepository(
        ReportingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task CompleteAsync(
        long jobId,
        string workerId,
        CompletedAccountStatementDocument completedDocument,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        AccountStatementJob job =
            await _dbContext.AccountStatementJobs.SingleAsync(
                candidate =>
                    candidate.Id == jobId &&
                    candidate.AccountStatementId ==
                        completedDocument.AccountStatementId &&
                    candidate.Status ==
                        AccountStatementJobStatuses.Processing &&
                    candidate.LockedBy == workerId,
                cancellationToken);

        GeneratedDocument? document =
            await _dbContext.GeneratedDocuments.SingleOrDefaultAsync(
                candidate => candidate.StorageKey ==
                    completedDocument.StorageKey,
                cancellationToken);

        if (document is null)
        {
            document = GeneratedDocument.Create(
                documentType: "AccountStatement",
                contentType: "application/pdf",
                fileName: completedDocument.FileName,
                storageKey: completedDocument.StorageKey,
                byteLength: completedDocument.ByteLength,
                sha256Hash: completedDocument.Sha256Hash,
                createdAt: completedAt);

            _dbContext.GeneratedDocuments.Add(document);
            await _dbContext.SaveChangesAsync(cancellationToken);
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
            await _dbContext.AccountStatementDocuments.AnyAsync(
                link =>
                    link.AccountStatementId ==
                        completedDocument.AccountStatementId &&
                    link.DocumentId == document.Id,
                cancellationToken);

        if (!relationExists)
        {
            _dbContext.AccountStatementDocuments.Add(
                AccountStatementDocument.Create(
                    completedDocument.AccountStatementId,
                    document.Id,
                    completedAt));
        }

        job.MarkCompleted(workerId, completedAt);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
