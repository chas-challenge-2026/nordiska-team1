using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Nordiska.Modules.Reporting.Infrastructure;

namespace Nordiska.Modules.Reporting.Tests;

public sealed class FileSystemReportDocumentStorageTests
{
    [Fact]
    public async Task SaveAndReadAsync_RoundTripsPdf()
    {
        string root = CreateTemporaryDirectory();

        try
        {
            var storage = CreateStorage(root);
            byte[] pdf = "%PDF-1.7 test"u8.ToArray();
            string hash = ComputeHash(pdf);

            await storage.SaveAsync(
                "tax-reports/1/document.pdf",
                pdf,
                hash,
                CancellationToken.None);

            byte[] result = await storage.ReadAsync(
                "tax-reports/1/document.pdf",
                hash,
                CancellationToken.None);

            Assert.Equal(pdf, result);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_StorageKeyLeavesRoot_ThrowsInvalidDataException()
    {
        string root = CreateTemporaryDirectory();

        try
        {
            var storage = CreateStorage(root);
            byte[] pdf = "%PDF-1.7 test"u8.ToArray();

            await Assert.ThrowsAsync<InvalidDataException>(() =>
                storage.SaveAsync(
                    "../outside.pdf",
                    pdf,
                    ComputeHash(pdf),
                    CancellationToken.None));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_HashDoesNotMatch_ThrowsInvalidDataException()
    {
        string root = CreateTemporaryDirectory();

        try
        {
            var storage = CreateStorage(root);
            byte[] pdf = "%PDF-1.7 test"u8.ToArray();

            await Assert.ThrowsAsync<InvalidDataException>(() =>
                storage.SaveAsync(
                    "tax-reports/1/document.pdf",
                    pdf,
                    new string('0', 64),
                    CancellationToken.None));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static FileSystemReportDocumentStorage CreateStorage(
        string root)
    {
        return new FileSystemReportDocumentStorage(
            Options.Create(
                new ReportDocumentStorageOptions
                {
                    RootPath = root
                }));
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(path);
        return path;
    }

    private static string ComputeHash(byte[] content)
    {
        return Convert.ToHexString(
                SHA256.HashData(content))
            .ToLowerInvariant();
    }
}
