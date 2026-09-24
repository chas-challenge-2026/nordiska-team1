using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Nordiska.Modules.Reporting.Infrastructure;

/// <summary>
/// PDF generator backed by the native C-compatible PDF generator.
/// </summary>
public sealed class PdfReportGenerator : IPdfReportGenerator
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DocumentCallback(
        IntPtr bytes,
        UIntPtr length,
        UIntPtr documentIndex,
        IntPtr context);

    [DllImport("nordiska_document_c_api", CallingConvention = CallingConvention.Cdecl)]
    private static extern int nordiska_document_generate_json(
        byte[] jsonUtf8,
        UIntPtr jsonLength,
        DocumentCallback callback,
        IntPtr callbackContext,
        StringBuilder errorBuffer,
        UIntPtr errorBufferLength);

    public Task<byte[]> GenerateTaxReportPdfAsync(
        TaxReportData data,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        cancellationToken.ThrowIfCancellationRequested();

        var json = JsonSerializer.SerializeToUtf8Bytes(new
        {
            account_number = data.AccountNumber,
            title = $"Tax report {data.Year}",
            summary_lines = new[]
            {
                $"Customer: {data.CustomerName}",
                $"Opening balance: {data.OpeningBalance:0.00}",
                $"Closing balance: {data.ClosingBalance:0.00}",
                $"Interest earned: {data.TotalInterestEarned:0.00}",
                $"Tax withheld: {data.TotalTaxWithheld:0.00}"
            },
            transactions = data.Transactions.Select(transaction => new
            {
                date = transaction.CreatedAt.ToString("yyyy-MM-dd"),
                type = transaction.Type,
                currency = "SEK",
                amount_minor = decimal.ToInt64(decimal.Round(transaction.Amount * 100m))
            })
        });

        using var output = new MemoryStream();
        DocumentCallback callback = (bytes, length, _, _) =>
        {
            var count = checked((int)length.ToUInt64());
            var buffer = new byte[count];
            Marshal.Copy(bytes, buffer, 0, count);
            output.Write(buffer, 0, buffer.Length);
            return 0;
        };
        var errorBuffer = new StringBuilder(1024);
        var status = nordiska_document_generate_json(
            json,
            (UIntPtr)json.Length,
            callback,
            IntPtr.Zero,
            errorBuffer,
            (UIntPtr)errorBuffer.Capacity);

        if (status != 0)
        {
            throw new InvalidOperationException(
                $"Native PDF generation failed with status {status}: {errorBuffer}");
        }

        return Task.FromResult(output.ToArray());
    }
}
