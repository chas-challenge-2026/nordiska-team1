using System.Threading;
using System.Threading.Tasks;

namespace Nordiska.Modules.Reporting.Infrastructure;

public interface IReportDataBuilder
{
    Task<TaxReportData> BuildAsync(
        long customerId,
        long accountId,
        int year,
        CancellationToken cancellationToken = default);
}
