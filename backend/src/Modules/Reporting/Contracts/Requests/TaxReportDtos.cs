using System;
using System.ComponentModel.DataAnnotations;

namespace Nordiska.Modules.Reporting.Contracts.Requests;

/// <summary>
/// Requests creation of an annual tax report for an account and tax year.
/// </summary>
public sealed record TaxReportRequest(
    [Required]
    long AccountId,

    [Range(2000, 2100)]
    int TaxYear
);

/// <summary>
/// Compatibility response for the existing in-memory report-job endpoints.
/// </summary>
public sealed record TaxReportJobResponse(
    string JobId,
    string Status,
    DateTime CreatedAt,
    string? DownloadUrl = null,
    string? Error = null
);
