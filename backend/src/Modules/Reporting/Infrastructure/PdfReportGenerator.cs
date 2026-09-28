using System.Collections.Generic;
using System.Globalization;
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
        var payload = new AnnualTaxReportPayload(
            Title: "Kontrolluppgift för ränteinkomst",
            TaxYear: data.Year.ToString(SwedishCulture),
            AccountNumber: data.AccountNumber,
            AccountName: data.AccountName,
            TotalInterestEarned: $"{data.TotalInterestEarned.ToString("N2", SwedishCulture)} SEK",
            PreliminaryTaxDeducted: $"{data.TotalTaxWithheld.ToString("N2", SwedishCulture)} SEK",
            ReportedToAuthority: "Skatteverket"
        );

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
}
