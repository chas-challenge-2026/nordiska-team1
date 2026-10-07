namespace Nordiska.Modules.Reporting.Domain;

public sealed class AccountStatement
{
    private readonly List<AccountStatementEntry> _entries = [];

    private AccountStatement()
    {
    }

    private AccountStatement(
        long customerId,
        long accountId,
        DateOnly periodStart,
        DateOnly periodEnd,
        string customerName,
        string accountNumber,
        string accountName,
        string currency,
        long openingBalanceMinor,
        long closingBalanceMinor,
        DateTimeOffset snapshotAt,
        string schemaVersion,
        string payloadHash,
        IReadOnlyList<AccountStatementEntryData> entries)
    {
        CustomerId = customerId;
        AccountId = accountId;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        CustomerName = customerName;
        AccountNumber = accountNumber;
        AccountName = accountName;
        Currency = currency;
        OpeningBalanceMinor = openingBalanceMinor;
        ClosingBalanceMinor = closingBalanceMinor;
        SnapshotAt = snapshotAt;
        SchemaVersion = schemaVersion;
        PayloadHash = payloadHash;

        foreach (AccountStatementEntryData entry in entries)
        {
            _entries.Add(AccountStatementEntry.Create(entry));
        }
    }

    public long Id { get; private set; }

    public long CustomerId { get; private set; }

    public long AccountId { get; private set; }

    public DateOnly PeriodStart { get; private set; }

    public DateOnly PeriodEnd { get; private set; }

    public string CustomerName { get; private set; } = null!;

    public string AccountNumber { get; private set; } = null!;

    public string AccountName { get; private set; } = null!;

    public string Currency { get; private set; } = null!;

    public long OpeningBalanceMinor { get; private set; }

    public long ClosingBalanceMinor { get; private set; }

    public DateTimeOffset SnapshotAt { get; private set; }

    public string SchemaVersion { get; private set; } = null!;

    public string PayloadHash { get; private set; } = null!;

    public IReadOnlyList<AccountStatementEntry> Entries => _entries;

    public static AccountStatement Create(
        long customerId,
        long accountId,
        DateOnly periodStart,
        DateOnly periodEnd,
        string customerName,
        string accountNumber,
        string accountName,
        string currency,
        long openingBalanceMinor,
        long closingBalanceMinor,
        DateTimeOffset snapshotAt,
        string schemaVersion,
        string payloadHash,
        IReadOnlyList<AccountStatementEntryData> entries)
    {
        return new AccountStatement(
            customerId,
            accountId,
            periodStart,
            periodEnd,
            customerName,
            accountNumber,
            accountName,
            currency,
            openingBalanceMinor,
            closingBalanceMinor,
            snapshotAt,
            schemaVersion,
            payloadHash,
            entries);
    }
}
