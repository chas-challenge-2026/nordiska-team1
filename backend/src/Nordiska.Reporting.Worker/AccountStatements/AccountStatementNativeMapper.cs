using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nordiska.Modules.Reporting.Domain;
using Nordiska.Reporting.Worker.TaxReports;

namespace Nordiska.Reporting.Worker.AccountStatements;

public sealed class AccountStatementNativeMapper
{
    private static readonly CultureInfo SwedishCulture =
        CultureInfo.GetCultureInfo("sv-SE");

    public string CreateJson(AccountStatement statement)
    {
        string documentId =
            $"account_statement_{statement.Id}";

        var document = new NativeAccountStatementDocument(
            Title: "Kontoutdrag",
            AccountNumber: statement.AccountNumber,
            AccountName: statement.AccountName,
            Currency: statement.Currency,
            Period:
                $"{statement.PeriodStart:yyyy-MM-dd} - " +
                $"{statement.PeriodEnd:yyyy-MM-dd}",
            OpeningBalance: FormatMoney(
                statement.OpeningBalanceMinor,
                statement.Currency),
            ClosingBalance: FormatMoney(
                statement.ClosingBalanceMinor,
                statement.Currency),
            Transactions: statement.Entries
                .OrderBy(entry => entry.SequenceNumber)
                .Select(entry =>
                    new NativeAccountStatementTransaction(
                        Date: entry.BookedAt.UtcDateTime.ToString(
                            "yyyy-MM-dd",
                            CultureInfo.InvariantCulture),
                        Type: entry.Type,
                        Description: entry.Description,
                        Currency: statement.Currency,
                        AmountMinor: entry.AmountMinor,
                        AmountDisplay: FormatSignedMoney(
                            entry.AmountMinor,
                            statement.Currency),
                        BalanceAfterDisplay: FormatMoney(
                            entry.BalanceAfterMinor,
                            statement.Currency)))
                .ToArray());

        var package = new NativeReportPackage(
            SchemaVersion: statement.SchemaVersion,
            CustomerId: statement.CustomerId,
            CustomerName: statement.CustomerName,
            CreatedAt: statement.SnapshotAt
                .UtcDateTime
                .ToString("O", CultureInfo.InvariantCulture),
            Documents:
            [
                new NativeDocumentEnvelope(
                    DocumentId: documentId,
                    Kind: "account_statement",
                    Version: "1.0",
                    Document: document)
            ]);

        return JsonSerializer.Serialize(package);
    }

    private static string FormatSignedMoney(
        long amountMinor,
        string currency)
    {
        string formatted = FormatMoney(amountMinor, currency);

        return amountMinor > 0
            ? $"+{formatted}"
            : formatted;
    }

    private static string FormatMoney(
        long amountMinor,
        string currency)
    {
        decimal amount = amountMinor / 100m;

        string formatted = amount
            .ToString("N2", SwedishCulture)
            .Replace('\u00A0', ' ')
            .Replace('\u202F', ' ');

        return $"{formatted} {currency}";
    }
}

internal sealed record NativeAccountStatementDocument(
    [property: JsonPropertyName("title")]
    string Title,

    [property: JsonPropertyName("account_number")]
    string AccountNumber,

    [property: JsonPropertyName("account_name")]
    string AccountName,

    [property: JsonPropertyName("currency")]
    string Currency,

    [property: JsonPropertyName("period")]
    string Period,

    [property: JsonPropertyName("opening_balance")]
    string OpeningBalance,

    [property: JsonPropertyName("closing_balance")]
    string ClosingBalance,

    [property: JsonPropertyName("transactions")]
    IReadOnlyList<NativeAccountStatementTransaction> Transactions);

internal sealed record NativeAccountStatementTransaction(
    [property: JsonPropertyName("date")]
    string Date,

    [property: JsonPropertyName("type")]
    string Type,

    [property: JsonPropertyName("description")]
    string Description,

    [property: JsonPropertyName("currency")]
    string Currency,

    [property: JsonPropertyName("amount_minor")]
    long AmountMinor,

    [property: JsonPropertyName("amount_display")]
    string AmountDisplay,

    [property: JsonPropertyName("balance_after_display")]
    string BalanceAfterDisplay);
