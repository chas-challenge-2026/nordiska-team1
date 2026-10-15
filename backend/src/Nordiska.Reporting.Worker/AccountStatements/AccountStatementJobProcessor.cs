using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Nordiska.Modules.Reporting.Application;
using Nordiska.Reporting.Worker.TaxReports;

namespace Nordiska.Reporting.Worker.AccountStatements;

public sealed class AccountStatementJobProcessor
{
    private readonly IAccountStatementJobRepository _jobs;
    private readonly IAccountStatementRenderPipeline _renderer;
    private readonly IReportDocumentStorage _storage;
    private readonly IAccountStatementCompletionRepository _completion;
    private readonly TimeProvider _timeProvider;
    private readonly TaxReportWorkerOptions _options;
    private readonly ILogger<AccountStatementJobProcessor> _logger;

    public AccountStatementJobProcessor(
        IAccountStatementJobRepository jobs,
        IAccountStatementRenderPipeline renderer,
        IReportDocumentStorage storage,
        IAccountStatementCompletionRepository completion,
        TimeProvider timeProvider,
        IOptions<TaxReportWorkerOptions> options,
        ILogger<AccountStatementJobProcessor> logger)
    {
        _jobs = jobs;
        _renderer = renderer;
        _storage = storage;
        _completion = completion;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> ProcessNextAsync(
        string workerId,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();

        ClaimedAccountStatementJob? job =
            await _jobs.ClaimNextAsync(
                workerId,
                now,
                TimeSpan.FromMinutes(
                    _options.LeaseDurationMinutes),
                cancellationToken);

        if (job is null)
        {
            return false;
        }

        try
        {
            RenderedAccountStatement rendered =
                await _renderer.RenderAsync(
                    job.AccountStatementId,
                    cancellationToken);

            string pdfHash = Convert
                .ToHexString(SHA256.HashData(rendered.PdfBytes))
                .ToLowerInvariant();

            string storageKey =
                $"account-statements/{rendered.AccountStatementId}/" +
                $"{pdfHash}.pdf";

            await _storage.SaveAsync(
                storageKey,
                rendered.PdfBytes,
                pdfHash,
                cancellationToken);

            var document = new CompletedAccountStatementDocument(
                rendered.AccountStatementId,
                rendered.FileName,
                storageKey,
                rendered.PdfBytes.LongLength,
                pdfHash);

            await _completion.CompleteAsync(
                job.JobId,
                workerId,
                document,
                _timeProvider.GetUtcNow(),
                cancellationToken);

            return true;
        }
        catch (Exception exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(
                exception,
                "Account statement job {JobId} failed.",
                job.JobId);

            await _jobs.MarkFailedAsync(
                job.JobId,
                workerId,
                exception.Message,
                _timeProvider.GetUtcNow(),
                TimeSpan.FromSeconds(
                    _options.RetryDelaySeconds),
                _options.MaximumAttempts,
                cancellationToken);

            return true;
        }
    }
}
