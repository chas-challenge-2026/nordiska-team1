using System.Text;
using Nordiska.Modules.Reporting.PdfGeneration;

namespace Nordiska.Modules.Reporting.Tests;

public sealed class PdfGenerationServiceTest
{
    [Fact]
    public void Generate_WithValidReport_ReturnsPdfDocument()
    {
        const string json = """
            {
              "account_number": "SE1234567890",
              "transactions": [
                {
                  "date": "2026-01-05",
                  "type": "deposit",
                  "currency": "SEK",
                  "amount_minor": 100000
                }
              ]
            }
            """;

        var pdf = new PdfGenerationService().Generate(json);

        Assert.NotNull(pdf);
        Assert.NotEmpty(pdf);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }
}
