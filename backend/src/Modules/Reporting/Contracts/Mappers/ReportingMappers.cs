using Nordiska.Modules.Reporting.Contracts.Responses;
using Nordiska.Modules.Reporting.Domain;

namespace Nordiska.Modules.Reporting.Contracts.Mappers;

public static class ReportingMappers
{
    public static TaxReportResponse ToResponse(this TaxReport report)
        => new(
            Id: report.Id,
            CustomerId: report.CustomerId,
            AccountId: report.AccountId,
            TaxYear: report.TaxYear,
            TotalInterestMinor: report.TotalInterestMinor,
            TaxDeductedMinor: report.TaxDeductedMinor,
            Currency: report.Currency,
            CustomerName: report.CustomerName,
            AccountNumber: report.AccountNumber,
            AccountName: report.AccountName,
            CreatedAt: report.CreatedAt,
            SchemaVersion: report.SchemaVersion);
}