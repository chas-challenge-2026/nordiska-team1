using Nordiska.Modules.Reporting.PdfGeneration;

var service = new PdfGenerationService();

Console.WriteLine("Starting");

string jsonPath = Path.Combine(
    AppContext.BaseDirectory,
    "sample-input-huge.json");

string json = File.ReadAllText(jsonPath);

GeneratedPdfBatch batch =
    service.GeneratePdfBatch(json);

int documentNumber = 1;

foreach (KeyValuePair<string, byte[]> document in batch.Documents)
{
    string path = Path.Combine(
        AppContext.BaseDirectory,
        $"testfil-{documentNumber}.pdf");

    File.WriteAllBytes(
        path,
        document.Value);

    Console.WriteLine(
        $"PDF {document.Key} at: {path}");

    documentNumber++;
}