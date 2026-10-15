using Nordiska.Modules.Reporting.Domain;

namespace Nordiska.Modules.Reporting.Application;

public interface IAnnualTaxReportRepository
{
    Task<TaxReport?> GetByIdAsync(
        long taxReportId,
        CancellationToken cancellationToken);

    Task<TaxReport?> GetByAccountAndYearAsync(
        long customerId,
        long accountId,
        int taxYear,
        CancellationToken cancellationToken);

    Task<TaxReportJob?> GetLatestJobAsync(
        long taxReportId,
        CancellationToken cancellationToken);

    Task<(TaxReport Report, TaxReportJob Job)>
        CreateReportAndJobAsync(
            TaxReport report,
            DateTimeOffset jobCreatedAt,
            CancellationToken cancellationToken);

    Task<TaxReportJob> CreateJobAsync(
        long taxReportId,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken);
}