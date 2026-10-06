namespace Nordiska.Modules.Reporting.Application;

public sealed record AccountStatementSourceEntry(
    long SourceLedgerEntryId,
    DateTimeOffset BookedAt,
    string Type,
    string Description,
    decimal Amount);

public sealed record AccountStatementSource(
    long CustomerId,
    long AccountId,
    string CustomerName,
    string AccountNumber,
    string AccountName,
    string Currency,
    decimal CurrentBalance,
    decimal AmountAfterPeriod,
    IReadOnlyList<AccountStatementSourceEntry> Entries);

public interface IAccountStatementSourceQuery
{
    Task<AccountStatementSource?> GetAsync(
        long customerId,
        long accountId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken);
}
