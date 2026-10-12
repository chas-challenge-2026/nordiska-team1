using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nordiska.Modules.Reporting.Infrastructure;

/// <summary>
/// PDF generator engine stub.
/// Implement the full native C++ / worker generator here.
/// </summary>
public sealed class PdfReportGenerator : IPdfReportGenerator
{
    public Task<byte[]> GenerateTaxReportPdfAsync(TaxReportData data, CancellationToken cancellationToken = default)
    {
        // Minimal PDF header stub so controller endpoints and tests can return valid PDF MIME content
        // Replace this with your C++ ProcessStartInfo / PInvoke call when ready!
        var minimalPdf = "%PDF-1.4\n1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] >>\nendobj\nxref\n0 4\n0000000000 65535 f \n0000000009 00000 n \n0000000058 00000 n \n0000000115 00000 n \ntrailer\n<< /Size 4 /Root 1 0 R >>\nstartxref\n185\n%%EOF\n";

        return Task.FromResult(Encoding.ASCII.GetBytes(minimalPdf));
    }
}
