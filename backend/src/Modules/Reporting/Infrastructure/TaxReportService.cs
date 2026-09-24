using System;
using System.Linq;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using Nordiska.BuildingBlocks.Database.Errors;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Reporting.Application;
using Nordiska.Modules.Reporting.Contracts.Requests;
using Nordiska.Modules.Reporting.Domain;

namespace Nordiska.Modules.Reporting.Infrastructure;

/// <summary>
/// Service coordinating tax report data extraction, job status tracking, and PDF document generation.
/// </summary>
public sealed class TaxReportService : ITaxReportService
{
    private readonly ISavingsAccountService _savingsAccountService;
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
