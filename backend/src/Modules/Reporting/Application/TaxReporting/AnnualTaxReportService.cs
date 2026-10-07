using Nordiska.Modules.Reporting.Domain;


namespace Nordiska.Modules.Reporting.Application;

public sealed record RequestAnnualTaxReportResult(
    long TaxReportId,
    long JobId,
    string Status,
    DateTimeOffset CreatedAt
);

public interface IAnnualTaxReportService
{
    Task<RequestAnnualTaxReportResult> RequestAsync(
        long customerId,
        long accountId,
        int taxYear,
        CancellationToken cancellationToken);
}

 

public sealed class AnnualTaxReportService
    : IAnnualTaxReportService
{
    private const string SchemaVersion = "1.0";

    private readonly IAnnualTaxCalculationQuery _calculationQuery;
    private readonly IAnnualTaxReportRepository _repository;
    private readonly ITaxReportPayloadHasher _payloadHasher;
    private readonly TimeProvider _timeProvider;

    public AnnualTaxReportService(
        IAnnualTaxCalculationQuery calculationQuery,
        IAnnualTaxReportRepository repository,
        ITaxReportPayloadHasher payloadHasher,
        TimeProvider timeProvider)
    {
        _calculationQuery = calculationQuery;
        _repository = repository;
        _payloadHasher = payloadHasher;
        _timeProvider = timeProvider;
    }

    public async Task<RequestAnnualTaxReportResult> RequestAsync(
        long customerId,
        long accountId,
        int taxYear,
        CancellationToken cancellationToken)
    {
        TaxReport? existingReport =
            await _repository.GetByAccountAndYearAsync(
                customerId,
                accountId,
                taxYear,
                cancellationToken);

        if (existingReport is not null)
        {
            TaxReportJob? existingJob =
                await _repository.GetLatestJobAsync(
                    existingReport.Id,
                    cancellationToken);

            if (existingJob is not null &&
                existingJob.Status != "Failed")
            {
                return MapResult(
                    existingReport,
                    existingJob);
            }

            TaxReportJob retryJob =
                await _repository.CreateJobAsync(
                    existingReport.Id,
                    _timeProvider.GetUtcNow(),
                    cancellationToken);

            return MapResult(
                existingReport,
                retryJob);
        }

        AnnualTaxCalculation? calculation =
            await _calculationQuery.GetAsync(
                customerId,
                accountId,
                taxYear,
                cancellationToken);

        if (calculation is null)
        {
            throw new KeyNotFoundException(
                "Kontot finns inte eller tillhör inte den inloggade kunden.");
        }

        DateTimeOffset createdAt =
            _timeProvider.GetUtcNow();

        var hashPayload =
            TaxReportHashPayload.From(
                calculation,
                createdAt,
                SchemaVersion);

        string payloadHash =
            _payloadHasher.Compute(hashPayload);

        TaxReport report =
            TaxReport.Create(
                calculation.CustomerId,
                calculation.AccountId,
                calculation.TaxYear,
                calculation.TotalInterestMinor,
                calculation.TaxDeductedMinor,
                calculation.Currency,
                calculation.CustomerName,
                calculation.AccountNumber,
                calculation.AccountName,
                createdAt,
                SchemaVersion,
                payloadHash);

        (TaxReport savedReport, TaxReportJob job) =
            await _repository.CreateReportAndJobAsync(
                report,
                createdAt,
                cancellationToken);

        return MapResult(
            savedReport,
            job);
    }

    private static RequestAnnualTaxReportResult MapResult(
        TaxReport report,
        TaxReportJob job)
    {
        return new RequestAnnualTaxReportResult(
            report.Id,
            job.Id,
            job.Status,
            job.CreatedAt);
    }
}
