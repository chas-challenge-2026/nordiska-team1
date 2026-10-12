namespace Nordiska.Modules.Reporting.Domain;

public sealed record AccountStatementEntryData(
    int SequenceNumber,
    long SourceLedgerEntryId,
    DateTimeOffset BookedAt,
    string Type,
    string Description,
    long AmountMinor,
    long BalanceAfterMinor);

public sealed class AccountStatementEntry
{
    private AccountStatementEntry()
    {
    }

    private AccountStatementEntry(
        AccountStatementEntryData data)
    {
        SequenceNumber = data.SequenceNumber;
        SourceLedgerEntryId = data.SourceLedgerEntryId;
        BookedAt = data.BookedAt;
        Type = data.Type;
        Description = data.Description;
        AmountMinor = data.AmountMinor;
        BalanceAfterMinor = data.BalanceAfterMinor;
    }

    public long Id { get; private set; }

    public long AccountStatementId { get; private set; }

    public int SequenceNumber { get; private set; }

    public long SourceLedgerEntryId { get; private set; }

    public DateTimeOffset BookedAt { get; private set; }

    public string Type { get; private set; } = null!;

    public string Description { get; private set; } = null!;

    public long AmountMinor { get; private set; }

    public long BalanceAfterMinor { get; private set; }

    public AccountStatement AccountStatement { get; private set; } = null!;

    internal static AccountStatementEntry Create(
        AccountStatementEntryData data)
    {
        return new AccountStatementEntry(data);
    }
}
