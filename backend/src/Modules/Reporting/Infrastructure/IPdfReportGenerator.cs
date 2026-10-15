using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nordiska.Modules.Banking.Contracts.Responses;

namespace Nordiska.Modules.Reporting.Infrastructure;

/// <summary>
/// Domain model containing all data necessary to render a signed tax report PDF.
/// </summary>
public record TaxReportData(
    long AccountId,
    string AccountNumber,
    string AccountName,
    long CustomerId,
    string CustomerName,
    string PersonalNum,
    int Year,
    decimal OpeningBalance,
    decimal ClosingBalance,
    decimal InterestRate,
    decimal TotalInterestEarned,
    decimal TotalTaxWithheld,
    IReadOnlyList<TransactionResponse> Transactions,
    DateTime GeneratedAt
);

/// <summary>
/// Interface for generating tax report PDF binaries (supports C# v1 implementation and C++ native v2 engine).
/// </summary>
public interface IPdfReportGenerator
{
    /// <summary>
    /// Generates a valid, cryptographically signed PDF document from structured tax report data.
    /// </summary>
    /// <param name="data">The tax report structured data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Raw PDF byte array with embedded signature/hash metadata.</returns>
    Task<byte[]> GenerateTaxReportPdfAsync(TaxReportData data, CancellationToken cancellationToken = default);
}
