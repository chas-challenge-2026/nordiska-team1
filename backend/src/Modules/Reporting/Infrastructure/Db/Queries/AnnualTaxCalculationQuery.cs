using Microsoft.EntityFrameworkCore;

namespace Nordiska.Modules.Reporting.Infrastructure.Db.Queries;

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

public sealed class AnnualTaxCalculationQuery
    : IAnnualTaxCalculationQuery
{
    private readonly ReportingDbContext _dbContext;

    public AnnualTaxCalculationQuery(
        ReportingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AnnualTaxCalculation?> GetAsync(
        long customerId,
        long accountId,
        int taxYear,
        CancellationToken cancellationToken)
    {
        var yearStart =
            new DateTimeOffset(
                taxYear,
                1,
                1,
                0,
                0,
                0,
                TimeSpan.Zero);

        var nextYearStart = yearStart.AddYears(1);

        var result = await _dbContext.Database
            .SqlQuery<AnnualTaxCalculation>($"""
                SELECT
                    c."Id" AS "CustomerId",
                    a."Id" AS "AccountId",
                    {taxYear} AS "TaxYear",

                    COALESCE(
                        SUM(t."AmountMinor")
                        FILTER (WHERE t."Type" = 'interest'),
                        0
                    )::bigint AS "TotalInterestMinor",

                    COALESCE(
                        SUM(ABS(t."AmountMinor"))
                        FILTER (WHERE t."Type" = 'tax'),
                        0
                    )::bigint AS "TaxDeductedMinor",

                    a."Currency" AS "Currency",
                    c."Name" AS "CustomerName",
                    a."AccountNumber" AS "AccountNumber",
                    a."Name" AS "AccountName"

                FROM banking."Accounts" a

                JOIN banking."Customers" c
                    ON c."Id" = a."CustomerId"

                LEFT JOIN banking."Transactions" t
                    ON t."AccountId" = a."Id"
                    AND t."BookedAt" >= {yearStart}
                    AND t."BookedAt" < {nextYearStart}

                WHERE
                    a."Id" = {accountId}
                    AND c."Id" = {customerId}

                GROUP BY
                    c."Id",
                    a."Id",
                    a."Currency",
                    c."Name",
                    a."AccountNumber",
                    a."Name"
                """)
            .SingleOrDefaultAsync(cancellationToken);

        return result;
    }
}