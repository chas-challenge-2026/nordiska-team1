using Microsoft.EntityFrameworkCore;
using Nordiska.Modules.Reporting.Application;

namespace Nordiska.Modules.Reporting.Infrastructure.Db.Queries;

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
                    NULL::bigint AS "AccountId",
                    {taxYear} AS "TaxYear",

                    COALESCE(
                        SUM(ROUND(t."Amount" * 100))
                        FILTER (WHERE t."Type" = 'interest'),
                        0
                    )::bigint AS "TotalInterestMinor",

                    COALESCE(
                        SUM(ABS(ROUND(t."Amount" * 100)))
                        FILTER (WHERE t."Type" = 'tax'),
                        0
                    )::bigint AS "TaxDeductedMinor",

                    MIN(a."CurrencyCode") AS "Currency",
                    c."Name" AS "CustomerName",
                    'ALL' AS "AccountNumber",
                    'Skatteunderlag ' || {taxYear} AS "AccountName"

                FROM banking.savings_accounts a

                JOIN banking.customers c
                    ON c."Id" = a."CustomerId"

                LEFT JOIN banking.ledger_entries t
                    ON t."AccountId" = a."Id"
                    AND NOT t."IsPlanned"
                    AND t."CreatedAt" >= {yearStart}
                    AND t."CreatedAt" < {nextYearStart}

                WHERE
                    c."Id" = {customerId}

                GROUP BY
                    c."Id",
                    c."Name",
                    {taxYear}
                HAVING COUNT(DISTINCT a."CurrencyCode") = 1
                """)
            .SingleOrDefaultAsync(cancellationToken);

        return result;
    }
}
