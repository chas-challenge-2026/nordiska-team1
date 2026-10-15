using System.Text.Json;
using Nordiska.Modules.Reporting.Domain;
using Nordiska.Reporting.Worker.AccountStatements;
using Nordiska.Reporting.Worker.TaxReports;

namespace Nordiska.Reporting.Worker.Tests;

public class NativeMapperTests
{
    private static readonly DateTimeOffset BookedAt =
        new(2026, 1, 10, 1, 0, 0, TimeSpan.FromHours(2));

    private static JsonElement Parse(string json)
        => JsonDocument.Parse(json).RootElement.Clone();

    private static JsonElement Document(JsonElement root)
        => root.GetProperty("documents")[0].GetProperty("document");

    [Fact]
    public void TaxReport_CreateJson_WrapsReportInEnvelope()
    {
        var report = ReportingTestData.NewTaxReport(customerId: 42);

        var root = Parse(new AnnualTaxReportNativeMapper().CreateJson(report));

        Assert.Equal("1.0", root.GetProperty("$schema_version").GetString());
        Assert.Equal(42, root.GetProperty("customer_id").GetInt64());
        Assert.Equal("Anna Svensson", root.GetProperty("customer_name").GetString());
        Assert.Equal("2026-01-15T09:00:00.0000000Z", root.GetProperty("created_at").GetString());

        var envelope = root.GetProperty("documents")[0];
        Assert.Equal($"annual_tax_report_{report.Id}", envelope.GetProperty("document_id").GetString());
        Assert.Equal("annual_tax_report", envelope.GetProperty("kind").GetString());
        Assert.Equal("1.0", envelope.GetProperty("version").GetString());
    }

    [Fact]
    public void TaxReport_CreateJson_FormatsAmountsInSwedishStyle()
    {
        var report = ReportingTestData.NewTaxReport();

        var document = Document(Parse(new AnnualTaxReportNativeMapper().CreateJson(report)));

        Assert.Equal("Kontrolluppgift för ränteinkomst", document.GetProperty("title").GetString());
        Assert.Equal("2025", document.GetProperty("tax_year").GetString());
        Assert.Equal("1234-567", document.GetProperty("account_number").GetString());
        Assert.Equal("Sparkonto", document.GetProperty("account_name").GetString());
        Assert.Equal("1 234,56 SEK", document.GetProperty("total_interest_earned").GetString());
        Assert.Equal("370,37 SEK", document.GetProperty("preliminary_tax_deducted").GetString());
    }

    [Fact]
    public void TaxReport_CreateJson_UnreportedReportSaysNotReported()
    {
        var report = ReportingTestData.NewTaxReport();

        var document = Document(Parse(new AnnualTaxReportNativeMapper().CreateJson(report)));

        Assert.Equal("Ej rapporterad", document.GetProperty("reported_to_authority").GetString());
    }

    [Fact]
    public void TaxReport_CreateJson_ReportedReportNamesTheAuthority()
    {
        var report = ReportingTestData.NewTaxReport();
        report.MarkReported("Skatteverket", "REF-1", DateTimeOffset.UtcNow);

        var document = Document(Parse(new AnnualTaxReportNativeMapper().CreateJson(report)));

        Assert.Equal("Skatteverket", document.GetProperty("reported_to_authority").GetString());
    }

    [Fact]
    public void AccountStatement_CreateJson_MapsHeaderAndEnvelope()
    {
        var statement = ReportingTestData.NewStatement(customerId: 42);

        var root = Parse(new AccountStatementNativeMapper().CreateJson(statement));

        Assert.Equal(42, root.GetProperty("customer_id").GetInt64());
        Assert.Equal("2026-02-01T08:00:00.0000000Z", root.GetProperty("created_at").GetString());

        var envelope = root.GetProperty("documents")[0];
        Assert.Equal($"account_statement_{statement.Id}", envelope.GetProperty("document_id").GetString());
        Assert.Equal("account_statement", envelope.GetProperty("kind").GetString());

        var document = Document(root);
        Assert.Equal("Kontoutdrag", document.GetProperty("title").GetString());
        Assert.Equal("1234-567", document.GetProperty("account_number").GetString());
        Assert.Equal("Sparkonto", document.GetProperty("account_name").GetString());
        Assert.Equal("SEK", document.GetProperty("currency").GetString());
        Assert.Equal("2026-01-01 - 2026-01-31", document.GetProperty("period").GetString());
        Assert.Equal("1 000,00 SEK", document.GetProperty("opening_balance").GetString());
        Assert.Equal("1 450,00 SEK", document.GetProperty("closing_balance").GetString());
        Assert.Equal(0, document.GetProperty("transactions").GetArrayLength());
    }

    [Fact]
    public void AccountStatement_CreateJson_OrdersTransactionsBySequenceNumber()
    {
        var statement = ReportingTestData.NewStatement(
            7,
            new AccountStatementEntryData(2, 11, BookedAt, "Deposit", "Second", 5000, 105000),
            new AccountStatementEntryData(1, 10, BookedAt, "Deposit", "First", 5000, 100000));

        var transactions = Document(Parse(new AccountStatementNativeMapper().CreateJson(statement)))
            .GetProperty("transactions");

        Assert.Equal("First", transactions[0].GetProperty("description").GetString());
        Assert.Equal("Second", transactions[1].GetProperty("description").GetString());
    }

    [Fact]
    public void AccountStatement_CreateJson_MapsTransactionFields()
    {
        var statement = ReportingTestData.NewStatement(
            7,
            new AccountStatementEntryData(1, 10, BookedAt, "Deposit", "Lön", 5000, 105000));

        var transaction = Document(Parse(new AccountStatementNativeMapper().CreateJson(statement)))
            .GetProperty("transactions")[0];

        // 01:00 at +02:00 is the previous day in UTC.
        Assert.Equal("2026-01-09", transaction.GetProperty("date").GetString());
        Assert.Equal("Deposit", transaction.GetProperty("type").GetString());
        Assert.Equal("Lön", transaction.GetProperty("description").GetString());
        Assert.Equal("SEK", transaction.GetProperty("currency").GetString());
        Assert.Equal(5000, transaction.GetProperty("amount_minor").GetInt64());
        Assert.Equal("+50,00 SEK", transaction.GetProperty("amount_display").GetString());
        Assert.Equal("1 050,00 SEK", transaction.GetProperty("balance_after_display").GetString());
    }

    [Fact]
    public void AccountStatement_CreateJson_OnlyPositiveAmountsGetPlusSign()
    {
        var statement = ReportingTestData.NewStatement(
            7,
            new AccountStatementEntryData(1, 10, BookedAt, "Withdrawal", "Uttag", -2000, 98000),
            new AccountStatementEntryData(2, 11, BookedAt, "Fee", "Avgift", 0, 98000));

        var transactions = Document(Parse(new AccountStatementNativeMapper().CreateJson(statement)))
            .GetProperty("transactions");

        var withdrawal = transactions[0].GetProperty("amount_display").GetString()!;
        Assert.EndsWith("20,00 SEK", withdrawal);
        Assert.DoesNotContain("+", withdrawal);
        Assert.Equal("0,00 SEK", transactions[1].GetProperty("amount_display").GetString());
    }
}
