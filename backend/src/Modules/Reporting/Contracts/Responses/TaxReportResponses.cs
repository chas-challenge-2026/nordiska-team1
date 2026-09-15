namespace Nordiska.Modules.Reporting.Contracts.Responses;

public record TaxReportResponse(
    long Id,
    long AccountId,
    int Year,
    string Status,
    string? DownloadUrl,
    string? Signature,
    DateTime CreatedAt
);
