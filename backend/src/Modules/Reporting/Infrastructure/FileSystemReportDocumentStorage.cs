using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Nordiska.Modules.Reporting.Application;

namespace Nordiska.Modules.Reporting.Infrastructure;

public sealed class ReportDocumentStorageOptions
{
    public string RootPath { get; set; } =
        "/var/lib/nordiska/report-documents";
}

public sealed class FileSystemReportDocumentStorage
    : IReportDocumentStorage
{
    private readonly string _rootPath;
    private readonly string _rootPrefix;

    public FileSystemReportDocumentStorage(
        IOptions<ReportDocumentStorageOptions> options)
    {
        _rootPath =
            Path.GetFullPath(options.Value.RootPath);

        _rootPrefix =
            _rootPath.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
    }

    public async Task SaveAsync(
        string storageKey,
        ReadOnlyMemory<byte> content,
        string expectedSha256Hash,
        CancellationToken cancellationToken)
    {
        VerifyHash(content.Span, expectedSha256Hash);

        string destinationPath =
            ResolveStoragePath(storageKey);

        Directory.CreateDirectory(
            Path.GetDirectoryName(destinationPath)!);

        if (File.Exists(destinationPath))
        {
            await VerifyExistingFileAsync(
                destinationPath,
                expectedSha256Hash,
                cancellationToken);

            return;
        }

        string temporaryPath =
            destinationPath + "." +
            Guid.NewGuid().ToString("N") +
            ".tmp";

        try
        {
            await File.WriteAllBytesAsync(
                temporaryPath,
                content.ToArray(),
                cancellationToken);

            try
            {
                File.Move(
                    temporaryPath,
                    destinationPath,
                    overwrite: false);
            }
            catch (IOException)
                when (File.Exists(destinationPath))
            {
                await VerifyExistingFileAsync(
                    destinationPath,
                    expectedSha256Hash,
                    cancellationToken);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public async Task<byte[]> ReadAsync(
        string storageKey,
        string expectedSha256Hash,
        CancellationToken cancellationToken)
    {
        string path =
            ResolveStoragePath(storageKey);

        byte[] content =
            await File.ReadAllBytesAsync(
                path,
                cancellationToken);

        VerifyHash(content, expectedSha256Hash);

        return content;
    }

    private string ResolveStoragePath(
        string storageKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            storageKey);

        string relativePath =
            storageKey.Replace(
                '/',
                Path.DirectorySeparatorChar);

        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidDataException(
                "Document storage key must be relative.");
        }

        string fullPath =
            Path.GetFullPath(
                Path.Combine(
                    _rootPath,
                    relativePath));

        if (!fullPath.StartsWith(
                _rootPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Document storage key leaves the storage root.");
        }

        return fullPath;
    }

    private static async Task VerifyExistingFileAsync(
        string path,
        string expectedHash,
        CancellationToken cancellationToken)
    {
        byte[] content =
            await File.ReadAllBytesAsync(
                path,
                cancellationToken);

        VerifyHash(content, expectedHash);
    }

    private static void VerifyHash(
        ReadOnlySpan<byte> content,
        string expectedHash)
    {
        string actualHash =
            Convert.ToHexString(
                    SHA256.HashData(content))
                .ToLowerInvariant();

        if (!string.Equals(
                actualHash,
                expectedHash,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Stored PDF hash verification failed.");
        }
    }
}