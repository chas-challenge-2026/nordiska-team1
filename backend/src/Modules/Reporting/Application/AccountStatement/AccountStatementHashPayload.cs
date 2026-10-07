using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Nordiska.Modules.Reporting.Domain;

namespace Nordiska.Modules.Reporting.Application;

public sealed record AccountStatementEntryHashPayload(
    int SequenceNumber,
    long SourceLedgerEntryId,
    DateTimeOffset BookedAt,
    string Type,
    string Description,
    long AmountMinor,
    long BalanceAfterMinor);

public sealed record AccountStatementHashPayload(
    long CustomerId,
    long AccountId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string CustomerName,
    string AccountNumber,
    string AccountName,
    string Currency,
    long OpeningBalanceMinor,
    long ClosingBalanceMinor,
    DateTimeOffset SnapshotAt,
    string SchemaVersion,
    IReadOnlyList<AccountStatementEntryHashPayload> Entries)
{
    public static AccountStatementHashPayload From(
        AccountStatement statement)
    {
        return new AccountStatementHashPayload(
            statement.CustomerId,
            statement.AccountId,
            statement.PeriodStart,
            statement.PeriodEnd,
            statement.CustomerName,
            statement.AccountNumber,
            statement.AccountName,
            statement.Currency,
            statement.OpeningBalanceMinor,
            statement.ClosingBalanceMinor,
            statement.SnapshotAt,
            statement.SchemaVersion,
            statement.Entries
                .OrderBy(entry => entry.SequenceNumber)
                .Select(entry =>
                    new AccountStatementEntryHashPayload(
                        entry.SequenceNumber,
                        entry.SourceLedgerEntryId,
                        entry.BookedAt,
                        entry.Type,
                        entry.Description,
                        entry.AmountMinor,
                        entry.BalanceAfterMinor))
                .ToArray());
    }
}

public interface IAccountStatementPayloadHasher
{
    string Compute(AccountStatementHashPayload payload);
}

public sealed class AccountStatementPayloadHasher
    : IAccountStatementPayloadHasher
{
    private const long TicksPerMicrosecond = 10;

    public string Compute(AccountStatementHashPayload payload)
    {
        using var stream = new MemoryStream();

        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("customerId", payload.CustomerId);
            writer.WriteNumber("accountId", payload.AccountId);
            writer.WriteString(
                "periodStart",
                payload.PeriodStart.ToString(
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture));
            writer.WriteString(
                "periodEnd",
                payload.PeriodEnd.ToString(
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture));
            writer.WriteString("customerName", payload.CustomerName);
            writer.WriteString("accountNumber", payload.AccountNumber);
            writer.WriteString("accountName", payload.AccountName);
            writer.WriteString("currency", payload.Currency);
            writer.WriteNumber(
                "openingBalanceMinor",
                payload.OpeningBalanceMinor);
            writer.WriteNumber(
                "closingBalanceMinor",
                payload.ClosingBalanceMinor);
            writer.WriteString(
                "snapshotAt",
                FormatTimestamp(payload.SnapshotAt));
            writer.WriteString("schemaVersion", payload.SchemaVersion);
            writer.WriteStartArray("entries");

            foreach (AccountStatementEntryHashPayload entry in
                     payload.Entries.OrderBy(item => item.SequenceNumber))
            {
                writer.WriteStartObject();
                writer.WriteNumber(
                    "sequenceNumber",
                    entry.SequenceNumber);
                writer.WriteNumber(
                    "sourceLedgerEntryId",
                    entry.SourceLedgerEntryId);
                writer.WriteString(
                    "bookedAt",
                    FormatTimestamp(entry.BookedAt));
                writer.WriteString("type", entry.Type);
                writer.WriteString("description", entry.Description);
                writer.WriteNumber("amountMinor", entry.AmountMinor);
                writer.WriteNumber(
                    "balanceAfterMinor",
                    entry.BalanceAfterMinor);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        byte[] hash = SHA256.HashData(stream.ToArray());

        return Convert
            .ToHexString(hash)
            .ToLowerInvariant();
    }

    private static string FormatTimestamp(DateTimeOffset value)
    {
        long utcTicks = value.UtcDateTime.Ticks;
        long normalizedTicks =
            utcTicks - utcTicks % TicksPerMicrosecond;

        return new DateTime(
                normalizedTicks,
                DateTimeKind.Utc)
            .ToString("O", CultureInfo.InvariantCulture);
    }
}
