using Nordiska.Modules.Reporting.PdfGeneration;


PdfGenerationService service = new();
Console.WriteLine($"starting");

var jsonPath = Path.Combine(
    AppContext.BaseDirectory,
    "sample-input-huge.json"
);
//nu
string jsonhuge = File.ReadAllText(jsonPath);

byte[] pdf = service.Generate(jsonhuge);

string path = Path.Combine(AppContext.BaseDirectory, "testfil.pdf");

File.WriteAllBytes(path, pdf);

Console.WriteLine($"PDF at: {path}");



