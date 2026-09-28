using Microsoft.Extensions.Logging;
using Nordiska.Modules.Reporting.Application;
using Nordiska.Modules.Reporting.Infrastructure;

namespace Nordiska.Reporting.Worker;

public sealed class TaxReportJobProcessor(
    ITaxReportJobRepository repository,
    IReportDataBuilder reportDataBuilder,
    IPdfReportGenerator pdfReportGenerator,
    IReportFileStorage fileStorage,
    ILogger<TaxReportJobProcessor> logger)
{
    private const int GenerationFailedErrorCode = 5001;

    public async Task<int> RequeueStaleAsync(
        TimeSpan staleAfter,
        CancellationToken cancellationToken)
    {
        return await repository.RequeueStaleProcessingAsync(staleAfter, cancellationToken);
    }

    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        var job = await repository.ClaimNextPendingAsync(cancellationToken);
        if (job is null)
        {
            return false;
        }

        try
        {
            var reportData = await reportDataBuilder.BuildAsync(
                job.CustomerId,
                job.AccountId,
                job.Year,
                cancellationToken);
            var pdf = await pdfReportGenerator.GenerateTaxReportPdfAsync(
                reportData,
                cancellationToken);

            await fileStorage.SaveAsync(job.Id, pdf, cancellationToken);

            job.Status = "Done";
            job.CompletedAt = DateTime.UtcNow;
            job.ErrorCode = null;
            job.ErrorMessage = null;
            job.UpdatedAt = DateTime.UtcNow;
            await repository.UpdateAsync(job, cancellationToken);

            logger.LogInformation("Completed tax report job {JobId}.", job.Id);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            job.Status = "Failed";
            job.CompletedAt = DateTime.UtcNow;
            job.ErrorCode = GenerationFailedErrorCode;
            job.ErrorMessage = exception.Message.Length <= 2000
                ? exception.Message
                : exception.Message[..2000];
            job.UpdatedAt = DateTime.UtcNow;

            await repository.UpdateAsync(job, CancellationToken.None);
            logger.LogError(exception, "Tax report job {JobId} failed.", job.Id);
        }

        return true;
    }
}
