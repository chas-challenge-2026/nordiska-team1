using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nordiska.Modules.Reporting.Domain;

namespace Nordiska.Reporting.Worker.TaxReports;

public sealed class AnnualTaxReportNativeMapper
{
    private static readonly CultureInfo SwedishCulture =
        CultureInfo.GetCultureInfo("sv-SE");

    public string CreateJson(
        TaxReport report)
    {
        string documentId =
            $"annual_tax_report_{report.Id}";

        var document =
            new NativeAnnualTaxReportDocument(
                Title: "Kontrolluppgift för ränteinkomst",
                TaxYear: report.TaxYear.ToString(
                    CultureInfo.InvariantCulture),
                AccountNumber: report.AccountNumber,
                AccountName: report.AccountName,
                TotalInterestEarned: FormatMoney(
                    report.TotalInterestMinor,
                    report.Currency),
                PreliminaryTaxDeducted: FormatMoney(
                    report.TaxDeductedMinor,
                    report.Currency),
                ReportedToAuthority:
                    report.ReportingAuthority
                    ?? "Ej rapporterad");

        var package =
            new NativeReportPackage(
                SchemaVersion: report.SchemaVersion,
                CustomerId: report.CustomerId,
                CustomerName: report.CustomerName,
                CreatedAt: report.CreatedAt
                    .UtcDateTime
                    .ToString(
                        "O",
                        CultureInfo.InvariantCulture),
                Documents:
                [
                    new NativeDocumentEnvelope(
                        DocumentId: documentId,
                        Kind: "annual_tax_report",
                        Version: "1.0",
                        Document: document)
                ]);

        return JsonSerializer.Serialize(package);
    }

    private static string FormatMoney(
        long amountMinor,
        string currency)
    {
        decimal amount = amountMinor / 100m;

        string formatted =
            amount.ToString(
                    "N2",
                    SwedishCulture)
                .Replace('\u00A0', ' ')
                .Replace('\u202F', ' ');

        return $"{formatted} {currency}";
    }
}

internal sealed record NativeReportPackage(
    [property: JsonPropertyName("$schema_version")]
    string SchemaVersion,

    [property: JsonPropertyName("customer_id")]
    long CustomerId,

    [property: JsonPropertyName("customer_name")]
    string CustomerName,

    [property: JsonPropertyName("created_at")]
    string CreatedAt,

    [property: JsonPropertyName("documents")]
    IReadOnlyList<NativeDocumentEnvelope> Documents);

internal sealed record NativeDocumentEnvelope(
    [property: JsonPropertyName("document_id")]
    string DocumentId,

    [property: JsonPropertyName("kind")]
    string Kind,

    [property: JsonPropertyName("version")]
    string Version,

    [property: JsonPropertyName("document")]
    object Document);

internal sealed record NativeAnnualTaxReportDocument(
    [property: JsonPropertyName("title")]
    string Title,

    [property: JsonPropertyName("tax_year")]
    string TaxYear,

    [property: JsonPropertyName("account_number")]
    string AccountNumber,

    [property: JsonPropertyName("account_name")]
    string AccountName,

    [property: JsonPropertyName("total_interest_earned")]
    string TotalInterestEarned,

    [property: JsonPropertyName("preliminary_tax_deducted")]
    string PreliminaryTaxDeducted,

    [property: JsonPropertyName("reported_to_authority")]
    string ReportedToAuthority);
