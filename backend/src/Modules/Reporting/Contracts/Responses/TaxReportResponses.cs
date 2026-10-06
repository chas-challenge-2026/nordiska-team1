namespace Nordiska.Modules.Reporting.Contracts.Responses;

public sealed record TaxReportResponse(
    long Id,
    long CustomerId,
    long AccountId,
    int TaxYear,
    long TotalInterestMinor,
    long TaxDeductedMinor,
    string Currency,
    string CustomerName,
    string AccountNumber,
    string AccountName,
    DateTimeOffset CreatedAt,
    string SchemaVersion
);


public sealed record TaxReportJobResponseAsync(
    long JobId,
    long TaxReportId,
    string Status,
    DateTimeOffset CreatedAt,
    long? DocumentId = null,
    string? Error = null
);