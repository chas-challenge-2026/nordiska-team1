using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Nordiska.Modules.Reporting.Application;

namespace Nordiska.Reporting.Worker.TaxReports;

public sealed class TaxReportJobProcessor
{
    private readonly ITaxReportJobRepository _jobs;
    private readonly ITaxReportRenderPipeline _renderer;
    private readonly IReportDocumentStorage _storage;
    private readonly ITaxReportCompletionRepository _completion;
    private readonly TimeProvider _timeProvider;
    private readonly TaxReportWorkerOptions _options;
    private readonly ILogger<TaxReportJobProcessor> _logger;

    public TaxReportJobProcessor(
        ITaxReportJobRepository jobs,
        ITaxReportRenderPipeline renderer,
        IReportDocumentStorage storage,
        ITaxReportCompletionRepository completion,
        TimeProvider timeProvider,
        IOptions<TaxReportWorkerOptions> options,
        ILogger<TaxReportJobProcessor> logger)
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
        DateTimeOffset now =
            _timeProvider.GetUtcNow();

        ClaimedTaxReportJob? job =
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
            RenderedTaxReport rendered =
                await _renderer.RenderAsync(
                    job.TaxReportId,
                    cancellationToken);

            string pdfHash =
                Convert.ToHexString(
                        SHA256.HashData(
                            rendered.PdfBytes))
                    .ToLowerInvariant();

            string storageKey =
                $"tax-reports/{rendered.TaxReportId}/" +
                $"{pdfHash}.pdf";

            await _storage.SaveAsync(
                storageKey,
                rendered.PdfBytes,
                pdfHash,
                cancellationToken);

            var document =
                new CompletedReportDocument(
                    rendered.TaxReportId,
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
                "Tax report job {JobId} failed.",
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