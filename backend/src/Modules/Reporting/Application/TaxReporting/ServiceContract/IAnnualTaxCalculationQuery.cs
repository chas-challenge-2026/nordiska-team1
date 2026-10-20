namespace Nordiska.Modules.Reporting.Application;

public sealed record AnnualTaxCalculation(
    long CustomerId,
    long AccountId,
    int TaxYear,
    long TotalInterestMinor,
    long TaxDeductedMinor,
    string Currency,
    string CustomerName,
    string AccountNumber,
    string AccountName
);

public interface IAnnualTaxCalculationQuery
{
    Task<AnnualTaxCalculation?> GetAsync(
        long customerId,
        long accountId,
        int taxYear,
        CancellationToken cancellationToken);
}