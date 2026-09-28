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
/// Domain model containing all data necessary to render a signed account statement PDF.
/// </summary>
public record StatementReportData(
    long AccountId,
    string AccountNumber,
    string AccountName,
    long CustomerId,
    string CustomerName,
    decimal OpeningBalance,
    decimal ClosingBalance,
    IReadOnlyList<TransactionResponse> Transactions,
    DateTime GeneratedAt
);

/// <summary>
/// Interface for generating report PDF binaries and archives (supports C++ native v2 engine).
/// </summary>
public interface IPdfReportGenerator
{
    /// <summary>
    /// Generates a valid, cryptographically signed PDF document from structured tax report data.
    /// </summary>
    Task<byte[]> GenerateTaxReportPdfAsync(TaxReportData data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a valid, cryptographically signed PDF account statement from structured statement data.
    /// </summary>
    Task<byte[]> GenerateStatementPdfAsync(StatementReportData data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a ZIP archive containing all account statements and tax reports for a customer.
    /// </summary>
    Task<byte[]> GenerateCustomerArchiveAsync(IReadOnlyList<TaxReportData> taxReports, IReadOnlyList<StatementReportData> statements, CancellationToken cancellationToken = default);
}
