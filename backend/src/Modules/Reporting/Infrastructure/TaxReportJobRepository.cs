using Microsoft.EntityFrameworkCore;
using Nordiska.Modules.Reporting.Application;
using Nordiska.Modules.Reporting.Domain;
using Nordiska.Modules.Reporting.Infrastructure.Db;

namespace Nordiska.Modules.Reporting.Infrastructure;

public sealed class TaxReportJobRepository(ReportingDbContext db)
    : ITaxReportJobRepository
{
    public Task<TaxReportJob?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return db.TaxReportJobs
            .SingleOrDefaultAsync(job => job.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<TaxReportJob>> GetPendingAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        return await db.TaxReportJobs
            .AsNoTracking()
            .Where(job => job.Status == "Pending")
            .OrderBy(job => job.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        TaxReportJob job,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        db.TaxReportJobs.Update(job);
        await db.SaveChangesAsync(cancellationToken);
    }
}
