using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using Nordiska.BuildingBlocks.Database.Errors;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Reporting.Application;
using Nordiska.Modules.Reporting.Contracts.Requests;

namespace Nordiska.Modules.Reporting.Infrastructure;

/// <summary>
/// Service coordinating tax report data extraction, job status tracking, and PDF document generation.
/// </summary>
public sealed class TaxReportService : ITaxReportService
{
    private readonly ISavingsAccountService _savingsAccountService;
    private readonly ICustomerService _customerService;
    private readonly ITransactionService _transactionService;
    private readonly IPdfReportGenerator _pdfGenerator;

    private static readonly ConcurrentDictionary<string, StoredReportJob> _jobs = new();

    public TaxReportService(
        ISavingsAccountService savingsAccountService,
        ICustomerService customerService,
        ITransactionService transactionService,
        IPdfReportGenerator pdfGenerator)
    {
        _savingsAccountService = savingsAccountService;
        _customerService = customerService;
        _transactionService = transactionService;
        _pdfGenerator = pdfGenerator;
    }

    public async Task<TaxReportJobResponse> CreateJobAsync(long customerId, long accountId, int year, CancellationToken cancellationToken = default)
    {
        var account = await _savingsAccountService.GetByIdAsync(accountId, cancellationToken)
            ?? throw new NotFoundException($"Account with ID {accountId} was not found.");

        if (account.CustomerId != customerId)
        {
            throw new AuthenticationException("Customer is not authorized for this account.");
        }

        var jobId = $"job_{Guid.NewGuid():N}";
        var job = new StoredReportJob
        {
            JobId = jobId,
            CustomerId = customerId,
            AccountId = accountId,
            Year = year,
            Status = "Processing",
            CreatedAt = DateTime.UtcNow,
            DownloadUrl = $"/api/reports/jobs/{jobId}/download"
        };

        _jobs[jobId] = job;

        // Process report generation asynchronously
        _ = Task.Run(async () =>
        {
            try
            {
                var reportData = await BuildReportDataAsync(customerId, accountId, year, CancellationToken.None);
                var pdfBytes = await _pdfGenerator.GenerateTaxReportPdfAsync(reportData, CancellationToken.None);

                job.PdfBytes = pdfBytes;
                job.FileName = $"skatteunderlag_{year}_{account.AccountNumber}.pdf";
                job.Status = "Completed";
                job.CompletedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                job.Status = "Failed";
                job.Error = ex.Message;
            }
        });

        return new TaxReportJobResponse(
            JobId: job.JobId,
            Status: job.Status,
            CreatedAt: job.CreatedAt,
            DownloadUrl: job.DownloadUrl
        );
    }

    public Task<TaxReportJobResponse?> GetJobStatusAsync(string jobId, long customerId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
        {
            return Task.FromResult<TaxReportJobResponse?>(null);
        }

        if (!isAdmin && job.CustomerId != customerId)
        {
            return Task.FromResult<TaxReportJobResponse?>(null);
        }

        return Task.FromResult<TaxReportJobResponse?>(new TaxReportJobResponse(
            JobId: job.JobId,
            Status: job.Status,
            CreatedAt: job.CreatedAt,
            DownloadUrl: job.Status == "Completed" ? job.DownloadUrl : null,
            Error: job.Error
        ));
    }

    public async Task<(byte[] FileBytes, string FileName)?> GetReportFileAsync(string jobId, long customerId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
        {
            return null;
        }

        if (!isAdmin && job.CustomerId != customerId)
        {
            return null;
        }

        if (job.PdfBytes != null && job.FileName != null)
        {
            return (job.PdfBytes, job.FileName);
        }

        // If still processing, wait briefly or generate on-demand
        var reportData = await BuildReportDataAsync(job.CustomerId, job.AccountId, job.Year, cancellationToken);
        var pdfBytes = await _pdfGenerator.GenerateTaxReportPdfAsync(reportData, cancellationToken);
        var fileName = $"skatteunderlag_{job.Year}_{reportData.AccountNumber}.pdf";

        job.PdfBytes = pdfBytes;
        job.FileName = fileName;
        job.Status = "Completed";

        return (pdfBytes, fileName);
    }

    public async Task<(byte[] FileBytes, string FileName)> GenerateDirectReportAsync(long customerId, long accountId, int year, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var account = await _savingsAccountService.GetByIdAsync(accountId, cancellationToken)
            ?? throw new NotFoundException($"Account with ID {accountId} was not found.");

        if (!isAdmin && account.CustomerId != customerId)
        {
            throw new AuthenticationException("Customer is not authorized for this account.");
        }

        var reportData = await BuildReportDataAsync(account.CustomerId, accountId, year, cancellationToken);
        var pdfBytes = await _pdfGenerator.GenerateTaxReportPdfAsync(reportData, cancellationToken);
        var fileName = $"skatteunderlag_{year}_{account.AccountNumber}.pdf";

        return (pdfBytes, fileName);
    }

    private async Task<TaxReportData> BuildReportDataAsync(long customerId, long accountId, int year, CancellationToken cancellationToken)
    {
        var account = await _savingsAccountService.GetByIdAsync(accountId, cancellationToken)
            ?? throw new NotFoundException($"Account with ID {accountId} was not found.");

        var customer = await _customerService.GetByIdAsync(customerId, cancellationToken)
            ?? throw new NotFoundException($"Customer with ID {customerId} was not found.");

        var transactions = (await _transactionService.QueryAsync(accountId, cancellationToken)).ToList();

        var yearStartDate = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var yearEndDate = new DateTime(year, 12, 31, 23, 59, 59, DateTimeKind.Utc);

        // Ledger amounts are already signed (withdrawals and outgoing transfers are negative)
        var openingBalance = transactions
            .Where(t => t.CreatedAt < yearStartDate)
            .Sum(t => t.Amount);

        var closingBalance = transactions
            .Where(t => t.CreatedAt <= yearEndDate)
            .Sum(t => t.Amount);

        // Estimate annual interest earned
        var interestEarned = Math.Round(closingBalance * account.InterestRate, 2);
        var taxWithheld = Math.Round(interestEarned * 0.30m, 2);

        return new TaxReportData(
            AccountId: account.Id,
            AccountNumber: account.AccountNumber,
            AccountName: account.AccountName ?? "Sparkonto",
            CustomerId: customer.Id,
            CustomerName: customer.Name,
            PersonalNum: customer.PersonalNum,
            Year: year,
            OpeningBalance: openingBalance,
            ClosingBalance: closingBalance,
            InterestRate: account.InterestRate,
            TotalInterestEarned: interestEarned,
            TotalTaxWithheld: taxWithheld,
            Transactions: transactions.OrderByDescending(t => t.CreatedAt).Take(20).ToList(),
            GeneratedAt: DateTime.UtcNow
        );
    }

    private sealed class StoredReportJob
    {
        public required string JobId { get; set; }
        public required long CustomerId { get; set; }
        public required long AccountId { get; set; }
        public required int Year { get; set; }
        public required string Status { get; set; }
        public required DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? DownloadUrl { get; set; }
        public string? Error { get; set; }
        public byte[]? PdfBytes { get; set; }
        public string? FileName { get; set; }
    }
}
