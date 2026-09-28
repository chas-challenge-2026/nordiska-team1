using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nordiska.Modules.Reporting.Infrastructure;

public sealed record StoredReportFile(byte[] Content, string FileName);

public interface IReportFileStorage
{
    Task SaveAsync(
        Guid jobId,
        byte[] content,
        CancellationToken cancellationToken = default);

    Task<StoredReportFile?> ReadAsync(
        Guid jobId,
        int year,
        CancellationToken cancellationToken = default);
}
