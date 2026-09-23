using Nordiska.Modules.Reporting.Application;

namespace Nordiska.Reporting.Worker.Service;

public sealed record TaxReportJobOverview(
    Guid JobId,
    long AccountId,
    int Year,
    string Status,
    string? DownloadUrl,
    int? ErrorCode,
    string? ErrorMessage);

public sealed record JobStatus(
    Guid JobId,
    string Status,
    string? DownloadUrl = null,
    int? ErrorCode = null,
    string? ErrorMessage = null);

public sealed record GenerateTaxReportPdf(
    Guid JobId);

public interface ITaxReportJobService
{
    Task<JobStatus?> GetJobStatusAsync(
        Guid jobId,
        CancellationToken cancellationToken = default);
}

public sealed class TaxReportJobService(
    ITaxReportJobRepository repository)
    : ITaxReportJobService
{
    public async Task<JobStatus?> GetJobStatusAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await repository.GetByIdAsync(jobId, cancellationToken);

        return job is null
            ? null
            : new JobStatus(
                job.Id,
                job.Status,
                job.DownloadUrl,
                job.ErrorCode,
                job.ErrorMessage);
    }

}