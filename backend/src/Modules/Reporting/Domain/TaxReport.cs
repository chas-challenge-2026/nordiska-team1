namespace Nordiska.Modules.Reporting.Domain;

public sealed class TaxReport
{
    private TaxReport()
    { }

    private TaxReport(
        long customerId,
        long accountId,
        int taxYear,
        long totalInterestMinor,
        long taxDeductedMinor,
        string currency,
        string customerName,
        string accountNumber,
        string accountName,
        DateTimeOffset createdAt,
        string schemaVersion,
        string payloadHash)
    {
        CustomerId = customerId;
        AccountId = accountId;
        TaxYear = taxYear;
        TotalInterestMinor = totalInterestMinor;
        TaxDeductedMinor = taxDeductedMinor;
        Currency = currency;
        CustomerName = customerName;
        AccountNumber = accountNumber;
        AccountName = accountName;
        CreatedAt = createdAt;
        SchemaVersion = schemaVersion;
        PayloadHash = payloadHash;
    }

    public long Id { get; private set; }

    public long CustomerId { get; private set; }

    public long AccountId { get; private set; }

    public int TaxYear { get; private set; }

    public long TotalInterestMinor { get; private set; }

    public long TaxDeductedMinor { get; private set; }

    public string Currency { get; private set; } = null!;

    public string CustomerName { get; private set; } = null!;

    public string AccountNumber { get; private set; } = null!;

    public string AccountName { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    public string SchemaVersion { get; private set; } = null!;


    public string PayloadHash { get; private set; } = null!;

    public static TaxReport Create(
        long customerId,
        long accountId,
        int taxYear,
        long totalInterestMinor,
        long taxDeductedMinor,
        string currency,
        string customerName,
        string accountNumber,
        string accountName,
        DateTimeOffset createdAt,
        string schemaVersion,
        string payloadHash)
    {
        return new TaxReport(
            customerId,
            accountId,
            taxYear,
            totalInterestMinor,
            taxDeductedMinor,
            currency,
            customerName,
            accountNumber,
            accountName,
            createdAt,
            schemaVersion,
            payloadHash);
    }
}