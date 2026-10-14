using System.Threading;
using System.Threading.Tasks;
using Nordiska.Modules.Reporting.Contracts.Requests;

namespace Nordiska.Modules.Reporting.Application;

/// <summary>
/// Service interface for handling customer tax reports and statement generation jobs.
/// </summary>
public interface ITaxReportService
{
    /// <summary>
    /// Initiates a tax report generation job for a savings account.
    /// </summary>
    /// <param name="customerId">The authenticated customer identifier requesting the report.</param>
    /// <param name="accountId">The target savings account identifier.</param>
    /// <param name="year">The tax year.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Job metadata containing the jobId and current status.</returns>
    Task<TaxReportJobResponse> CreateJobAsync(long customerId, long accountId, int year, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the status and download metadata for an existing report job.
    /// </summary>
    /// <param name="jobId">The unique report job identifier.</param>
    /// <param name="customerId">The authenticated customer identifier.</param>
    /// <param name="isAdmin">Whether the caller has administrative privileges.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current job status or null if not found/forbidden.</returns>
    Task<TaxReportJobResponse?> GetJobStatusAsync(string jobId, long customerId, bool isAdmin, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the generated binary PDF report and attachment filename.
    /// </summary>
    /// <param name="jobId">The unique report job identifier.</param>
    /// <param name="customerId">The authenticated customer identifier.</param>
    /// <param name="isAdmin">Whether the caller has administrative privileges.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tuple of byte array and suggested filename, or null if not found.</returns>
    Task<(byte[] FileBytes, string FileName)?> GetReportFileAsync(string jobId, long customerId, bool isAdmin, CancellationToken cancellationToken = default);

    /// <summary>
    /// Directly generates a tax report PDF for immediate synchronous download (v1 MVP direct route).
    /// </summary>
    /// <param name="customerId">The authenticated customer identifier.</param>
    /// <param name="accountId">The target savings account identifier.</param>
    /// <param name="year">The tax year.</param>
    /// <param name="isAdmin">Whether the caller has administrative privileges.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tuple of PDF byte array and suggested filename.</returns>
    Task<(byte[] FileBytes, string FileName)> GenerateDirectReportAsync(long customerId, long accountId, int year, bool isAdmin, CancellationToken cancellationToken = default);
}
