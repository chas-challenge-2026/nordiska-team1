using Nordiska.Modules.Reporting.Domain;

namespace Nordiska.Modules.Reporting.Application;

public interface ITaxReportJobRepository
{
    Task<TaxReportJob?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TaxReportJob>> GetPendingAsync(
        int limit,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        TaxReportJob job,
        CancellationToken cancellationToken = default);
}
