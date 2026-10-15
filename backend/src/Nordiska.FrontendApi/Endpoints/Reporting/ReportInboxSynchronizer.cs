using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nordiska.Modules.Inbox.Application;
using Nordiska.Modules.Reporting.Infrastructure.Db;

namespace Nordiska.FrontendApi.Endpoints.Reporting;

public sealed class ReportInboxSynchronizer : IReportInboxSynchronizer
{
    private readonly ReportingDbContext _reportingDb;
    private readonly IInboxService _inboxService;
    private readonly ILogger<ReportInboxSynchronizer> _logger;

    public ReportInboxSynchronizer(
        ReportingDbContext reportingDb,
        IInboxService inboxService,
        ILogger<ReportInboxSynchronizer> logger)
    {
        _reportingDb = reportingDb;
        _inboxService = inboxService;
        _logger = logger;
    }

    public async Task SyncTaxReportAsync(long customerId, long taxReportId, CancellationToken cancellationToken = default)
    {
        try
        {
            var report = await (
                from tr in _reportingDb.TaxReports.AsNoTracking()
                join link in _reportingDb.TaxReportDocuments.AsNoTracking() on tr.Id equals link.TaxReportId
                join gen in _reportingDb.GeneratedDocuments.AsNoTracking() on link.DocumentId equals gen.Id
                where tr.CustomerId == customerId && tr.Id == taxReportId
                orderby link.CreatedAt descending
                select new
                {
                    tr.TaxYear,
                    gen.FileName,
                    gen.ContentType,
                    gen.StorageKey,
                    gen.ByteLength,
                    gen.Sha256Hash
                }
            ).FirstOrDefaultAsync(cancellationToken);

            if (report is null)
            {
                return;
            }

            await _inboxService.RegisterCustomerDocumentAsync(
                customerId: customerId,
                documentType: "TaxReport",
                title: $"Skatteunderlag {report.TaxYear}",
                fileName: report.FileName,
                mimeType: report.ContentType,
                storageKey: report.StorageKey,
                fileSizeBytes: report.ByteLength,
                sha256: report.Sha256Hash,
                sourceType: "ReportingModule",
                sourceId: $"tax-report:{taxReportId}",
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync tax report {TaxReportId} to inbox for customer {CustomerId}", taxReportId, customerId);
        }
    }

    public async Task SyncAccountStatementAsync(long customerId, long accountStatementId, CancellationToken cancellationToken = default)
    {
        try
        {
            var stmt = await (
                from s in _reportingDb.AccountStatements.AsNoTracking()
                join link in _reportingDb.AccountStatementDocuments.AsNoTracking() on s.Id equals link.AccountStatementId
                join gen in _reportingDb.GeneratedDocuments.AsNoTracking() on link.DocumentId equals gen.Id
                where s.CustomerId == customerId && s.Id == accountStatementId
                orderby link.CreatedAt descending
                select new
                {
                    s.PeriodStart,
                    s.PeriodEnd,
                    gen.FileName,
                    gen.ContentType,
                    gen.StorageKey,
                    gen.ByteLength,
                    gen.Sha256Hash
                }
            ).FirstOrDefaultAsync(cancellationToken);

            if (stmt is null)
            {
                return;
            }

            await _inboxService.RegisterCustomerDocumentAsync(
                customerId: customerId,
                documentType: "AccountStatement",
                title: $"Kontoutdrag {stmt.PeriodStart:yyyy-MM-dd} - {stmt.PeriodEnd:yyyy-MM-dd}",
                fileName: stmt.FileName,
                mimeType: stmt.ContentType,
                storageKey: stmt.StorageKey,
                fileSizeBytes: stmt.ByteLength,
                sha256: stmt.Sha256Hash,
                sourceType: "ReportingModule",
                sourceId: $"account-statement:{accountStatementId}",
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync account statement {AccountStatementId} to inbox for customer {CustomerId}", accountStatementId, customerId);
        }
    }

    public async Task SyncAllCustomerReportsAsync(long customerId, CancellationToken cancellationToken = default)
    {
        try
        {
            var taxReports = await (
                from tr in _reportingDb.TaxReports.AsNoTracking()
                join link in _reportingDb.TaxReportDocuments.AsNoTracking() on tr.Id equals link.TaxReportId
                join gen in _reportingDb.GeneratedDocuments.AsNoTracking() on link.DocumentId equals gen.Id
                where tr.CustomerId == customerId
                orderby link.CreatedAt descending
                select new
                {
                    tr.Id,
                    tr.TaxYear,
                    gen.FileName,
                    gen.ContentType,
                    gen.StorageKey,
                    gen.ByteLength,
                    gen.Sha256Hash
                }
            ).ToListAsync(cancellationToken);

            foreach (var report in taxReports)
            {
                await _inboxService.RegisterCustomerDocumentAsync(
                    customerId: customerId,
                    documentType: "TaxReport",
                    title: $"Skatteunderlag {report.TaxYear}",
                    fileName: report.FileName,
                    mimeType: report.ContentType,
                    storageKey: report.StorageKey,
                    fileSizeBytes: report.ByteLength,
                    sha256: report.Sha256Hash,
                    sourceType: "ReportingModule",
                    sourceId: $"tax-report:{report.Id}",
                    cancellationToken: cancellationToken);
            }

            var accountStatements = await (
                from s in _reportingDb.AccountStatements.AsNoTracking()
                join link in _reportingDb.AccountStatementDocuments.AsNoTracking() on s.Id equals link.AccountStatementId
                join gen in _reportingDb.GeneratedDocuments.AsNoTracking() on link.DocumentId equals gen.Id
                where s.CustomerId == customerId
                orderby link.CreatedAt descending
                select new
                {
                    s.Id,
                    s.PeriodStart,
                    s.PeriodEnd,
                    gen.FileName,
                    gen.ContentType,
                    gen.StorageKey,
                    gen.ByteLength,
                    gen.Sha256Hash
                }
            ).ToListAsync(cancellationToken);

            foreach (var stmt in accountStatements)
            {
                await _inboxService.RegisterCustomerDocumentAsync(
                    customerId: customerId,
                    documentType: "AccountStatement",
                    title: $"Kontoutdrag {stmt.PeriodStart:yyyy-MM-dd} - {stmt.PeriodEnd:yyyy-MM-dd}",
                    fileName: stmt.FileName,
                    mimeType: stmt.ContentType,
                    storageKey: stmt.StorageKey,
                    fileSizeBytes: stmt.ByteLength,
                    sha256: stmt.Sha256Hash,
                    sourceType: "ReportingModule",
                    sourceId: $"account-statement:{stmt.Id}",
                    cancellationToken: cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync all reports to inbox for customer {CustomerId}", customerId);
        }
    }
}