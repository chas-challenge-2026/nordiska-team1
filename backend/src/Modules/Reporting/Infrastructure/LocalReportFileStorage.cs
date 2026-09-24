using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Nordiska.Modules.Reporting.Infrastructure;

public sealed class LocalReportFileStorage : IReportFileStorage
{
    private readonly string _rootDirectory;

    public LocalReportFileStorage(IConfiguration configuration)
    {
        var configuredPath = configuration["Reporting:FileStoragePath"]
            ?? "generated-reports";
        _rootDirectory = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(Path.Combine(FindRepositoryRoot(), configuredPath));
    }

    public async Task SaveAsync(
        Guid jobId,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        Directory.CreateDirectory(_rootDirectory);

        var path = GetPath(jobId);
        await File.WriteAllBytesAsync(path, content, cancellationToken);
    }

    public async Task<StoredReportFile?> ReadAsync(
        Guid jobId,
        int year,
        CancellationToken cancellationToken = default)
    {
        var path = GetPath(jobId);
        if (!File.Exists(path))
        {
            return null;
        }

        var content = await File.ReadAllBytesAsync(path, cancellationToken);
        return new StoredReportFile(content, $"skatteunderlag_{year}.pdf");
    }

    private string GetPath(Guid jobId)
    {
        return Path.Combine(_rootDirectory, $"{jobId:N}.pdf");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
