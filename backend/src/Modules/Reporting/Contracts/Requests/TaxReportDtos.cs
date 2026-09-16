using System;
using System.ComponentModel.DataAnnotations;

namespace Nordiska.Modules.Reporting.Contracts.Requests;

/// <summary>
/// Request payload to generate a tax report (årsbesked / skatteunderlag) for a specific account and year.
/// </summary>
/// <param name="AccountId">The unique identifier of the savings account.</param>
/// <param name="Year">The tax year to generate report for (e.g. 2024, 2025, 2026).</param>
public record TaxReportRequest(
    [Required]
    long AccountId,

    [Range(2000, 2100)]
    int Year
);

/// <summary>
/// Response payload for an asynchronous tax report job.
/// </summary>
/// <param name="JobId">Unique identifier of the generated report job.</param>
/// <param name="Status">Current status of the job ('Pending', 'Processing', 'Completed', 'Failed').</param>
/// <param name="CreatedAt">Timestamp when the job was requested (UTC).</param>
/// <param name="DownloadUrl">Relative URL to download the report once completed.</param>
/// <param name="Error">Optional error details if the report generation failed.</param>
public record TaxReportJobResponse(
    string JobId,
    string Status,
    DateTime CreatedAt,
    string? DownloadUrl = null,
    string? Error = null
);
