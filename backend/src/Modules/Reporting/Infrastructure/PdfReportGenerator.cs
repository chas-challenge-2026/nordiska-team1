using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Nordiska.Modules.Reporting.PdfGeneration;

namespace Nordiska.Modules.Reporting.Infrastructure;

/// <summary>
/// PDF generator engine utilizing the native C++ generator via PdfGenerationService.
/// </summary>
public sealed class PdfReportGenerator : IPdfReportGenerator
{
    private static readonly CultureInfo SwedishCulture = new("sv-SE");
    private readonly PdfGenerationService _pdfGenerationService;

    public PdfReportGenerator(PdfGenerationService pdfGenerationService)
    {
        _pdfGenerationService = pdfGenerationService;
    }

    public Task<byte[]> GenerateTaxReportPdfAsync(TaxReportData data, CancellationToken cancellationToken = default)
    {
        var payload = BuildTaxReportPayload(data);

        var envelope = new CustomerBatchEnvelope(
            SchemaVersion: "1.0",
            CustomerId: (ulong)data.CustomerId,
            CustomerName: data.CustomerName,
            CreatedAt: data.GeneratedAt.ToString("o", CultureInfo.InvariantCulture),
            Documents: new List<PdfDocumentEnvelope>
            {
                new(
                    DocumentId: $"tax-{data.Year}-{data.AccountId}",
                    Kind: "annual_tax_report",
                    Version: "1.0",
                    Document: payload
                )
            }
        );

        var json = JsonSerializer.Serialize(envelope);
        var pdfBytes = _pdfGenerationService.Generate(json);

        return Task.FromResult(pdfBytes);
    }

    public Task<byte[]> GenerateStatementPdfAsync(StatementReportData data, CancellationToken cancellationToken = default)
    {
        var payload = BuildStatementPayload(data);

        var envelope = new CustomerBatchEnvelope(
            SchemaVersion: "1.0",
            CustomerId: (ulong)data.CustomerId,
            CustomerName: data.CustomerName,
            CreatedAt: data.GeneratedAt.ToString("o", CultureInfo.InvariantCulture),
            Documents: new List<PdfDocumentEnvelope>
            {
                new(
                    DocumentId: $"statement-{data.AccountId}",
                    Kind: "account_statement",
                    Version: "1.0",
                    Document: payload
                )
            }
        );

        var json = JsonSerializer.Serialize(envelope);
        var pdfBytes = _pdfGenerationService.Generate(json);

        return Task.FromResult(pdfBytes);
    }

    public Task<byte[]> GenerateCustomerArchiveAsync(
        IReadOnlyList<TaxReportData> taxReports,
        IReadOnlyList<StatementReportData> statements,
        CancellationToken cancellationToken = default)
    {
        if (taxReports.Count == 0 && statements.Count == 0)
        {
            throw new InvalidOperationException("No documents available to generate archive.");
        }

        var customerId = taxReports.Count > 0 ? (ulong)taxReports[0].CustomerId : (ulong)statements[0].CustomerId;
        var customerName = taxReports.Count > 0 ? taxReports[0].CustomerName : statements[0].CustomerName;

        var documentEnvelopes = new List<PdfDocumentEnvelope>();

        foreach (var tax in taxReports)
        {
            documentEnvelopes.Add(new PdfDocumentEnvelope(
                DocumentId: $"skatteunderlag_{tax.Year}_{tax.AccountNumber}",
                Kind: "annual_tax_report",
                Version: "1.0",
                Document: BuildTaxReportPayload(tax)
            ));
        }

        foreach (var stmt in statements)
        {
            documentEnvelopes.Add(new PdfDocumentEnvelope(
                DocumentId: $"kontoutdrag_{stmt.AccountNumber}",
                Kind: "account_statement",
                Version: "1.0",
                Document: BuildStatementPayload(stmt)
            ));
        }

        var batchEnvelope = new CustomerBatchEnvelope(
            SchemaVersion: "1.0",
            CustomerId: customerId,
            CustomerName: customerName,
            CreatedAt: DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            Documents: documentEnvelopes
        );

        var json = JsonSerializer.Serialize(batchEnvelope);
        var generatedPdfs = _pdfGenerationService.GenerateBatch(json);

        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var kvp in generatedPdfs)
            {
                var entry = archive.CreateEntry($"{kvp.Key}.pdf", CompressionLevel.Fastest);
                using var entryStream = entry.Open();
                entryStream.Write(kvp.Value, 0, kvp.Value.Length);
            }
        }

        return Task.FromResult(memoryStream.ToArray());
    }

    private static AnnualTaxReportPayload BuildTaxReportPayload(TaxReportData data)
    {
        return new AnnualTaxReportPayload(
            Title: "Kontrolluppgift för ränteinkomst",
            TaxYear: data.Year.ToString(SwedishCulture),
            AccountNumber: data.AccountNumber,
            AccountName: data.AccountName,
            TotalInterestEarned: $"{data.TotalInterestEarned.ToString("N2", SwedishCulture)} SEK",
            PreliminaryTaxDeducted: $"{data.TotalTaxWithheld.ToString("N2", SwedishCulture)} SEK",
            ReportedToAuthority: "Skatteverket"
        );
    }

    private static AccountStatementPayload BuildStatementPayload(StatementReportData data)
    {
        var transactions = data.Transactions
            .OrderByDescending(t => t.CreatedAt)
            .Take(50)
            .Select(t => new StatementTransactionPayload(
                Date: t.CreatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Type: t.Type,
                Description: t.Label ?? (t.Amount < 0 ? "Uttag" : "Insättning"),
                Currency: "SEK",
                AmountMinor: (long)Math.Round(t.Amount * 100),
                AmountDisplay: $"{(t.Amount > 0 ? "+" : "")}{t.Amount.ToString("N2", SwedishCulture)} SEK",
                BalanceAfterDisplay: ""
            ))
            .ToList();

        return new AccountStatementPayload(
            Title: "Kontoutdrag",
            AccountNumber: data.AccountNumber,
            AccountName: data.AccountName,
            Currency: "SEK",
            Period: $"{DateTime.UtcNow.AddMonths(-12):yyyy-MM-dd} - {DateTime.UtcNow:yyyy-MM-dd}",
            OpeningBalance: $"{data.OpeningBalance.ToString("N2", SwedishCulture)} SEK",
            ClosingBalance: $"{data.ClosingBalance.ToString("N2", SwedishCulture)} SEK",
            Transactions: transactions
        );
    }
}
