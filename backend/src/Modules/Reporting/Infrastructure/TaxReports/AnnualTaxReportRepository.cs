using Microsoft.EntityFrameworkCore;
using Nordiska.Modules.Reporting.Application;
using Nordiska.Modules.Reporting.Domain;
using Nordiska.Modules.Reporting.Infrastructure.Db;

namespace Nordiska.Modules.Reporting.Infrastructure;

public sealed class AnnualTaxReportRepository
    : IAnnualTaxReportRepository
{
    private readonly ReportingDbContext _dbContext;

    public AnnualTaxReportRepository(
        ReportingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<TaxReport?> GetByIdAsync(
        long taxReportId,
        CancellationToken cancellationToken)
    {
        return _dbContext.TaxReports
            .AsNoTracking()
            .SingleOrDefaultAsync(
                report => report.Id == taxReportId,
                cancellationToken);
    }

    public Task<TaxReport?> GetByAccountAndYearAsync(
        long customerId,
        long accountId,
        int taxYear,
        CancellationToken cancellationToken)
    {
        return _dbContext.TaxReports
            .AsNoTracking()
            .SingleOrDefaultAsync(
                report =>
                    report.CustomerId == customerId &&
                    report.AccountId == accountId &&
                    report.TaxYear == taxYear,
                cancellationToken);
    }

    public Task<TaxReportJob?> GetLatestJobAsync(
        long taxReportId,
        CancellationToken cancellationToken)
    {
        return _dbContext.TaxReportJobs
            .AsNoTracking()
            .Where(job =>
                job.TaxReportId == taxReportId)
            .OrderByDescending(job => job.CreatedAt)
            .ThenByDescending(job => job.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(TaxReport Report, TaxReportJob Job)>
        CreateReportAndJobAsync(
            TaxReport report,
            DateTimeOffset jobCreatedAt,
            CancellationToken cancellationToken)
    {
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        _dbContext.TaxReports.Add(report);

        // Behövs för att databasen ska tilldela report.Id.
        await _dbContext.SaveChangesAsync(
            cancellationToken);

        TaxReportJob job =
            TaxReportJob.Create(
                report.Id,
                jobCreatedAt);

        _dbContext.TaxReportJobs.Add(job);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return (report, job);
    }

    public async Task<TaxReportJob> CreateJobAsync(
        long taxReportId,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        TaxReportJob job =
            TaxReportJob.Create(
                taxReportId,
                createdAt);

        _dbContext.TaxReportJobs.Add(job);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return job;
    }
}