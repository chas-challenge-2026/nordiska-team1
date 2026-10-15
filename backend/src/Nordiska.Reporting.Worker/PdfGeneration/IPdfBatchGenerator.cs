namespace Nordiska.Modules.Reporting.PdfGeneration;

public interface IPdfBatchGenerator
{
    GeneratedPdfBatch GeneratePdfBatch(string json);
}