using Microsoft.EntityFrameworkCore;
using Nordiska.Modules.Reporting.Application;

namespace Nordiska.Modules.Reporting.Infrastructure.Db.Queries;

public sealed class AccountStatementSourceQuery
    : IAccountStatementSourceQuery
{
    private readonly ReportingDbContext _dbContext;

    public AccountStatementSourceQuery(
        ReportingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AccountStatementSource?> GetAsync(
        long customerId,
        long accountId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken)
    {
        DateTimeOffset periodStart = new(
            fromDate.ToDateTime(
                TimeOnly.MinValue,
                DateTimeKind.Utc));

        DateTimeOffset periodEndExclusive = new(
            toDate.AddDays(1).ToDateTime(
                TimeOnly.MinValue,
                DateTimeKind.Utc));

        AccountStatementSourceHeader? header =
            await _dbContext.Database
                .SqlQuery<AccountStatementSourceHeader>($"""
                    SELECT
                        c."Id" AS "CustomerId",
                        a."Id" AS "AccountId",
                        c."Name" AS "CustomerName",
                        a."AccountNumber" AS "AccountNumber",
                        COALESCE(a."AccountName", a."AccountType") AS "AccountName",
                        a."CurrencyCode" AS "Currency",
                        a."Balance" AS "CurrentBalance",
                        COALESCE(
                            (
                                SELECT SUM(entry."Amount")
                                FROM banking.ledger_entries entry
                                WHERE entry."AccountId" = a."Id"
                                  AND NOT entry."IsPlanned"
                                  AND entry."CreatedAt" >= {periodEndExclusive}
                            ),
                            0
                        ) AS "AmountAfterPeriod"
                    FROM banking.savings_accounts a
                    JOIN banking.customers c
                        ON c."Id" = a."CustomerId"
                    WHERE a."Id" = {accountId}
                      AND c."Id" = {customerId}
                    """)
                .SingleOrDefaultAsync(cancellationToken);

        if (header is null)
        {
            return null;
        }

        List<AccountStatementSourceEntry> entries =
            await _dbContext.Database
                .SqlQuery<AccountStatementSourceEntry>($"""
                    SELECT
                        entry."Id" AS "SourceLedgerEntryId",
                        entry."CreatedAt" AS "BookedAt",
                        entry."Type" AS "Type",
                        COALESCE(NULLIF(entry."Label", ''), entry."Type") AS "Description",
                        entry."Amount" AS "Amount"
                    FROM banking.ledger_entries entry
                    WHERE entry."AccountId" = {accountId}
                      AND NOT entry."IsPlanned"
                      AND entry."CreatedAt" >= {periodStart}
                      AND entry."CreatedAt" < {periodEndExclusive}
                    ORDER BY entry."CreatedAt", entry."Id"
                    """)
                .ToListAsync(cancellationToken);

        return new AccountStatementSource(
            header.CustomerId,
            header.AccountId,
            header.CustomerName,
            header.AccountNumber,
            header.AccountName,
            header.Currency,
            header.CurrentBalance,
            header.AmountAfterPeriod,
            entries);
    }

    private sealed record AccountStatementSourceHeader(
        long CustomerId,
        long AccountId,
        string CustomerName,
        string AccountNumber,
        string AccountName,
        string Currency,
        decimal CurrentBalance,
        decimal AmountAfterPeriod);
}
