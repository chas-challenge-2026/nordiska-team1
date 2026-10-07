using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Nordiska.Modules.Reporting.Domain;

namespace Nordiska.Modules.Reporting.Application;

public sealed record TaxReportHashPayload(
    long CustomerId,
    long AccountId,
    int TaxYear,
    long TotalInterestMinor,
    long TaxDeductedMinor,
    string Currency,
    string CustomerName,
    string AccountNumber,
    string AccountName,
    DateTimeOffset CreatedAt,
    string SchemaVersion)
{
    public static TaxReportHashPayload From(
        AnnualTaxCalculation calculation,
        DateTimeOffset createdAt,
        string schemaVersion)
    {
        return new TaxReportHashPayload(
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
            schemaVersion);
    }

    public static TaxReportHashPayload From(
        TaxReport report)
    {
        return new TaxReportHashPayload(
            report.CustomerId,
            report.AccountId,
            report.TaxYear,
            report.TotalInterestMinor,
            report.TaxDeductedMinor,
            report.Currency,
            report.CustomerName,
            report.AccountNumber,
            report.AccountName,
            report.CreatedAt,
            report.SchemaVersion);
    }
}

public interface ITaxReportPayloadHasher
{
    string Compute(TaxReportHashPayload payload);
}

public sealed class TaxReportPayloadHasher
    : ITaxReportPayloadHasher
{
    private const long TicksPerMicrosecond = 10;

    public string Compute(
        TaxReportHashPayload payload)
    {
        using var stream = new MemoryStream();

        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();

            writer.WriteNumber(
                "customerId",
                payload.CustomerId);

            writer.WriteNumber(
                "accountId",
                payload.AccountId);

            writer.WriteNumber(
                "taxYear",
                payload.TaxYear);

            writer.WriteNumber(
                "totalInterestMinor",
                payload.TotalInterestMinor);

            writer.WriteNumber(
                "taxDeductedMinor",
                payload.TaxDeductedMinor);

            writer.WriteString(
                "currency",
                payload.Currency);

            writer.WriteString(
                "customerName",
                payload.CustomerName);

            writer.WriteString(
                "accountNumber",
                payload.AccountNumber);

            writer.WriteString(
                "accountName",
                payload.AccountName);

            writer.WriteString(
                "createdAt",
                NormalizePostgresTimestamp(
                        payload.CreatedAt)
                    .ToString(
                        "O",
                        CultureInfo.InvariantCulture));

            writer.WriteString(
                "schemaVersion",
                payload.SchemaVersion);

            writer.WriteEndObject();
        }

        byte[] hash =
            SHA256.HashData(stream.ToArray());

        return Convert
            .ToHexString(hash)
            .ToLowerInvariant();
    }

    private static DateTime NormalizePostgresTimestamp(
        DateTimeOffset value)
    {
        long utcTicks = value.UtcDateTime.Ticks;
        long normalizedTicks =
            utcTicks - utcTicks % TicksPerMicrosecond;

        return new DateTime(
            normalizedTicks,
            DateTimeKind.Utc);
    }
}
