using Nordiska.Modules.Reporting.Domain;
using Nordiska.Modules.Reporting.Contracts.Responses;

namespace Nordiska.Modules.Reporting.Contracts.Mappers;

public static class ReportingMappers
{
    public static TaxReportResponse ToResponse(this TaxReport r)
        => new(r.Id, r.AccountId, r.Year, r.Status, r.DownloadUrl, r.Signature, r.CreatedAt);
}
