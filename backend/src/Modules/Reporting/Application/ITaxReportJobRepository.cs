using Nordiska.Modules.Reporting.Domain;

namespace Nordiska.Modules.Reporting.Application;

public interface ITaxReportJobRepository
{
    Task CreateAsync(
        TaxReportJob job,
        CancellationToken cancellationToken = default);

    Task<TaxReportJob?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<TaxReportJob?> GetActiveAsync(
        long customerId,
        long accountId,
        int year,
        CancellationToken cancellationToken = default);

    Task<TaxReportJob?> ClaimNextPendingAsync(
        CancellationToken cancellationToken = default);

    Task<int> RequeueStaleProcessingAsync(
        TimeSpan staleAfter,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        TaxReportJob job,
        CancellationToken cancellationToken = default);
}
