using System.Threading;
using System.Threading.Tasks;

namespace Nordiska.FrontendApi.Endpoints.Reporting;

public interface IReportInboxSynchronizer
{
    Task SyncTaxReportAsync(long customerId, long taxReportId, CancellationToken cancellationToken = default);
    Task SyncAccountStatementAsync(long customerId, long accountStatementId, CancellationToken cancellationToken = default);
    Task SyncAllCustomerReportsAsync(long customerId, CancellationToken cancellationToken = default);
}