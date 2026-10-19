namespace Nordiska.Reporting.Worker.JobHandlers.PdfGeneration;

public interface IPdfBatchGenerator
{
    GeneratedPdfBatch GeneratePdfBatch(string json);
}
