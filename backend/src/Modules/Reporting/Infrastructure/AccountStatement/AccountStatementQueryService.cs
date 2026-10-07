using Microsoft.EntityFrameworkCore;
using Nordiska.Modules.Reporting.Application;
using Nordiska.Modules.Reporting.Domain;
using Nordiska.Modules.Reporting.Infrastructure.Db;

namespace Nordiska.Modules.Reporting.Infrastructure;

public sealed class AccountStatementQueryService
    : IAccountStatementQueryService
{
    private readonly ReportingDbContext _dbContext;
    private readonly IReportDocumentStorage _storage;

    public AccountStatementQueryService(
        ReportingDbContext dbContext,
        IReportDocumentStorage storage)
    {
        _dbContext = dbContext;
        _storage = storage;
    }

    public Task<AccountStatementJobStatus?> GetStatusAsync(
        long customerId,
        long jobId,
        CancellationToken cancellationToken)
    {
        return (
            from job in _dbContext.AccountStatementJobs.AsNoTracking()
            join statement in _dbContext.AccountStatements.AsNoTracking()
                on job.AccountStatementId equals statement.Id
            where job.Id == jobId &&
                  statement.CustomerId == customerId
            select new AccountStatementJobStatus(
                job.Id,
                statement.Id,
                job.Status,
                job.CreatedAt,
                job.CompletedAt,
                job.Status == AccountStatementJobStatuses.Failed
                    ? "PDF generation failed."
                    : null))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<AccountStatementDownload?> DownloadAsync(
        long customerId,
        long accountStatementId,
        CancellationToken cancellationToken)
    {
        DocumentMetadata? document =
            await (
                from statement in _dbContext.AccountStatements.AsNoTracking()
                join link in _dbContext.AccountStatementDocuments.AsNoTracking()
                    on statement.Id equals link.AccountStatementId
                join generated in _dbContext.GeneratedDocuments.AsNoTracking()
                    on link.DocumentId equals generated.Id
                where statement.Id == accountStatementId &&
                      statement.CustomerId == customerId
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

        return new AccountStatementDownload(
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
