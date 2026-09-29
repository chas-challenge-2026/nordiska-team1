using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using Nordiska.BuildingBlocks.Database.Errors;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Contracts.Responses;
using Nordiska.Modules.Banking.Domain;
using Nordiska.Modules.Reporting.Application;
using Nordiska.Modules.Reporting.Contracts.Requests;
using Nordiska.Modules.Reporting.Domain;

namespace Nordiska.Modules.Reporting.Infrastructure;

public sealed class TaxReportService : ITaxReportService
{
    private readonly ISavingsAccountService _savingsAccountService;
    private readonly ICustomerService _customerService;
    private readonly ITransactionService _transactionService;
    private readonly IPdfReportGenerator _pdfGenerator;
    private readonly ITaxReportJobRepository? _jobRepository;
    private readonly IReportFileStorage? _fileStorage;
    private readonly IReportDataBuilder _reportDataBuilder;

    public TaxReportService(
        ISavingsAccountService savingsAccountService,
        ICustomerService customerService,
        ITransactionService transactionService,
        IPdfReportGenerator pdfGenerator,
        ITaxReportJobRepository? jobRepository = null,
        IReportFileStorage? fileStorage = null,
        IReportDataBuilder? reportDataBuilder = null)
    {
        _savingsAccountService = savingsAccountService;
        _customerService = customerService;
        _transactionService = transactionService;
        _pdfGenerator = pdfGenerator;
        _jobRepository = jobRepository;
        _fileStorage = fileStorage;
        _reportDataBuilder = reportDataBuilder
            ?? new ReportDataBuilder(savingsAccountService, customerService, transactionService);
    }

    public async Task<TaxReportJobResponse> CreateJobAsync(long customerId, long accountId, int year, CancellationToken cancellationToken = default)
    {
        var account = await _savingsAccountService.GetByIdAsync(accountId, cancellationToken)
            ?? throw new NotFoundException($"Account with ID {accountId} was not found.");

        if (account.CustomerId != customerId)
        {
            throw new AuthenticationException("Customer is not authorized for this account.");
        }

        EnsureQueuedJobDependencies();

        var existingJob = await _jobRepository!.GetActiveAsync(
            customerId,
            accountId,
            year,
            cancellationToken);
        if (existingJob is not null)
        {
            return ToResponse(existingJob);
        }

        var jobId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;
        var entity = new TaxReportJob
        {
            Id = jobId,
            CustomerId = customerId,
            AccountId = accountId,
            Year = year,
            Status = "Pending",
            DownloadUrl = $"/api/reports/jobs/{jobId}/download",
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };

        await _jobRepository.CreateAsync(entity, cancellationToken);
        return ToResponse(entity);
    }

    public Task<TaxReportJobResponse?> GetJobStatusAsync(string jobId, long customerId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        EnsureQueuedJobDependencies();
        if (!Guid.TryParse(jobId, out var parsedJobId))
        {
            return Task.FromResult<TaxReportJobResponse?>(null);
        }

        return GetJobStatusFromRepositoryAsync(parsedJobId, customerId, isAdmin, cancellationToken);
    }

    private async Task<TaxReportJobResponse?> GetJobStatusFromRepositoryAsync(
        Guid jobId,
        long customerId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var job = await _jobRepository!.GetByIdAsync(jobId, cancellationToken);
        if (job is null || (!isAdmin && job.CustomerId != customerId))
        {
            return null;
        }

        return ToResponse(job);
    }

    public async Task<(byte[] FileBytes, string FileName)?> GetReportFileAsync(string jobId, long customerId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        EnsureQueuedJobDependencies();
        if (!Guid.TryParse(jobId, out var parsedJobId))
        {
            return null;
        }

        var job = await _jobRepository!.GetByIdAsync(parsedJobId, cancellationToken);
        if (job is null || (!isAdmin && job.CustomerId != customerId))
        {
            return null;
        }

        if (job.Status != "Done")
        {
            return null;
        }

        var file = await _fileStorage!.ReadAsync(job.Id, job.Year, cancellationToken);
        return file is null ? null : (file.Content, file.FileName);
    }

    public async Task<(byte[] FileBytes, string FileName)> GenerateDirectReportAsync(long customerId, long accountId, int year, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var account = await _savingsAccountService.GetByIdAsync(accountId, cancellationToken)
            ?? throw new NotFoundException($"Account with ID {accountId} was not found.");

        if (!isAdmin && account.CustomerId != customerId)
        {
            throw new AuthenticationException("Customer is not authorized for this account.");
        }

        var reportData = await _reportDataBuilder.BuildAsync(account.CustomerId, accountId, year, cancellationToken);
        var pdfBytes = await _pdfGenerator.GenerateTaxReportPdfAsync(reportData, cancellationToken);
        var fileName = $"skatteunderlag_{year}_{account.AccountNumber}.pdf";

        return (pdfBytes, fileName);
    }

    public async Task<(byte[] FileBytes, string FileName)> GenerateDirectStatementAsync(long customerId, long accountId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var account = await _savingsAccountService.GetByIdAsync(accountId, cancellationToken)
            ?? throw new NotFoundException($"Account with ID {accountId} was not found.");

        if (!isAdmin && account.CustomerId != customerId)
        {
            throw new AuthenticationException("Customer is not authorized for this account.");
        }

        var customer = await _customerService.GetByIdAsync(account.CustomerId, cancellationToken)
            ?? throw new NotFoundException($"Customer with ID {customerId} was not found.");

        var transactions = (await _transactionService.QueryAsync(accountId, cancellationToken)).ToList();
        var statementData = BuildStatementData(customer, account, transactions);
        var pdfBytes = await _pdfGenerator.GenerateStatementPdfAsync(statementData, cancellationToken);
        var fileName = $"kontoutdrag_{account.AccountNumber}.pdf";

        return (pdfBytes, fileName);
    }

    public async Task<(byte[] FileBytes, string FileName)> GenerateCustomerArchiveAsync(long customerId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var customer = await _customerService.GetByIdAsync(customerId, cancellationToken)
            ?? throw new NotFoundException($"Customer with ID {customerId} was not found.");

        var accounts = (await _savingsAccountService.GetByCustomerIdAsync(customerId, cancellationToken)).ToList();
        if (accounts.Count == 0)
        {
            throw new InvalidOperationException("No accounts found for customer.");
        }

        var currentYear = DateTime.UtcNow.Year;
        var taxReports = new List<TaxReportData>();
        var statements = new List<StatementReportData>();

        foreach (var account in accounts)
        {
            var transactions = (await _transactionService.QueryAsync(account.Id, cancellationToken)).ToList();
            taxReports.Add(BuildReportDataInternal(customer, account, currentYear, transactions));
            statements.Add(BuildStatementData(customer, account, transactions));
        }

        var zipBytes = await _pdfGenerator.GenerateCustomerArchiveAsync(taxReports, statements, cancellationToken);
        var fileName = $"nordiska_dokument_{customerId}.zip";

        return (zipBytes, fileName);
    }

    private static StatementReportData BuildStatementData(Customer customer, SavingsAccountResponse account, List<TransactionResponse> transactions)
    {
        var closingBalance = account.Balance;
        var openingBalance = closingBalance - transactions.Sum(t => t.Amount);

        return new StatementReportData(
            AccountId: account.Id,
            AccountNumber: account.AccountNumber,
            AccountName: account.AccountName ?? "Sparkonto",
            CustomerId: customer.Id,
            CustomerName: customer.Name,
            OpeningBalance: openingBalance,
            ClosingBalance: closingBalance,
            Transactions: transactions,
            GeneratedAt: DateTime.UtcNow
        );
    }

    private static TaxReportData BuildReportDataInternal(Customer customer, SavingsAccountResponse account, int year, List<TransactionResponse> transactions)
    {
        var yearStartDate = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var yearEndDate = new DateTime(year, 12, 31, 23, 59, 59, DateTimeKind.Utc);

        var openingBalance = transactions
            .Where(t => t.CreatedAt < yearStartDate)
            .Sum(t => t.Amount);

        var closingBalance = transactions
            .Where(t => t.CreatedAt <= yearEndDate)
            .Sum(t => t.Amount);

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

    private void EnsureQueuedJobDependencies()
    {
        if (_jobRepository is null || _fileStorage is null)
        {
            throw new InvalidOperationException("Queued report dependencies are not configured.");
        }
    }

    private static TaxReportJobResponse ToResponse(Domain.TaxReportJob job)
    {
        return new TaxReportJobResponse(
            JobId: job.Id.ToString(),
            Status: job.Status,
            CreatedAt: job.CreatedAt,
            DownloadUrl: job.Status == "Done" ? job.DownloadUrl : null,
            Error: job.ErrorMessage);
    }
}
