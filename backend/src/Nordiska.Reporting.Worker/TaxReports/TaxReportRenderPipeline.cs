using Nordiska.Modules.Reporting.Application;
using Nordiska.Modules.Reporting.Domain;
using Nordiska.Modules.Reporting.PdfGeneration;

namespace Nordiska.Reporting.Worker.TaxReports;

public sealed record RenderedTaxReport(
    long TaxReportId,
    long CustomerId,
    string DocumentId,
    string FileName,
    byte[] PdfBytes);

public interface ITaxReportRenderPipeline
{
    Task<RenderedTaxReport> RenderAsync(
        long taxReportId,
        CancellationToken cancellationToken);
}

public sealed class TaxReportRenderPipeline
    : ITaxReportRenderPipeline
{
    private readonly IAnnualTaxReportRepository _reports;
    private readonly ITaxReportSnapshotVerifier _verifier;
    private readonly AnnualTaxReportNativeMapper _mapper;
    private readonly IPdfBatchGenerator _generator;

    public TaxReportRenderPipeline(
        IAnnualTaxReportRepository reports,
        ITaxReportSnapshotVerifier verifier,
        AnnualTaxReportNativeMapper mapper,
        IPdfBatchGenerator generator)
    {
        _reports = reports;
        _verifier = verifier;
        _mapper = mapper;
        _generator = generator;
    }

    public async Task<RenderedTaxReport> RenderAsync(
        long taxReportId,
        CancellationToken cancellationToken)
    {
        TaxReport report =
            await _reports.GetByIdAsync(
                taxReportId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"Tax report {taxReportId} was not found.");

        _verifier.Verify(report);

        string json = _mapper.CreateJson(report);

        GeneratedPdfBatch batch =
            _generator.GeneratePdfBatch(json);

        ulong expectedCustomerId =
            checked((ulong)report.CustomerId);

        if (batch.CustomerId != expectedCustomerId)
        {
            throw new InvalidDataException(
                "Native PDF batch returned the wrong customer ID.");
        }

        string expectedDocumentId =
            $"annual_tax_report_{report.Id}";

        if (!batch.Documents.TryGetValue(
                expectedDocumentId,
                out byte[]? pdfBytes))
        {
            throw new InvalidDataException(
                $"Native PDF batch did not contain " +
                $"{expectedDocumentId}.");
        }

        if (pdfBytes.Length < 5 ||
            !pdfBytes.AsSpan().StartsWith("%PDF-"u8))
        {
            throw new InvalidDataException(
                "Native generator returned invalid PDF bytes.");
        }

        return new RenderedTaxReport(
            report.Id,
            report.CustomerId,
            expectedDocumentId,
            $"arsbesked-{report.TaxYear}-" +
            $"{report.AccountNumber}.pdf",
            pdfBytes);
    }
}