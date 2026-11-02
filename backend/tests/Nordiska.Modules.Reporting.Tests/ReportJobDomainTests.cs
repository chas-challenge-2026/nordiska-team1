using Nordiska.Modules.Reporting.Domain;
using Xunit;

namespace Nordiska.Modules.Reporting.Tests;

public sealed class ReportJobDomainTests
{
    private static readonly DateTimeOffset Created =
        new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TaxReportJob_ClaimAndComplete_TracksOwnershipAndState()
    {
        TaxReportJob job = TaxReportJob.Create(7, Created);
        job.Claim("worker-1", Created, TimeSpan.FromMinutes(5));

        Assert.Equal(TaxReportJobStatuses.Processing, job.Status);
        Assert.Equal(1, job.AttemptCount);

        job.MarkCompleted("worker-1", Created.AddMinutes(1));

        Assert.Equal(TaxReportJobStatuses.Completed, job.Status);
        Assert.Null(job.LockedBy);
        Assert.NotNull(job.CompletedAt);
    }

    [Fact]
    public void TaxReportJob_ExpiredLeaseCanBeClaimedAndRetryCanBeScheduled()
    {
        TaxReportJob job = TaxReportJob.Create(7, Created);
        job.Claim("worker-1", Created, TimeSpan.FromMinutes(1));
        job.Claim("worker-2", Created.AddMinutes(1), TimeSpan.FromMinutes(1));

        job.MarkFailed("worker-2", new string('x', 3000), Created.AddMinutes(2), TimeSpan.FromMinutes(10), 3);

        Assert.Equal(TaxReportJobStatuses.Pending, job.Status);
        Assert.Equal(2000, job.LastError!.Length);
        Assert.Equal(Created.AddMinutes(12), job.AvailableAt);
    }

    [Fact]
    public void TaxReportJob_FinalFailureAndInvalidOperationsAreRejected()
    {
        TaxReportJob job = TaxReportJob.Create(7, Created);

        Assert.Throws<ArgumentException>(() => job.Claim("", Created, TimeSpan.FromMinutes(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => job.Claim("worker", Created, TimeSpan.Zero));
        Assert.Throws<InvalidOperationException>(() => job.MarkCompleted("worker", Created));

        job.Claim("worker", Created, TimeSpan.FromMinutes(1));
        job.MarkFailed("worker", "failure", Created.AddMinutes(1), TimeSpan.Zero, 1);

        Assert.Equal(TaxReportJobStatuses.Failed, job.Status);
        Assert.Throws<InvalidOperationException>(() => job.MarkCompleted("worker", Created));

        TaxReportJob invalidRetry = TaxReportJob.Create(8, Created);
        invalidRetry.Claim("worker", Created, TimeSpan.FromMinutes(1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            invalidRetry.MarkFailed("worker", "failure", Created, TimeSpan.Zero, 0));
    }

    [Fact]
    public void AccountStatementJob_ClaimAndCompleteTracksState()
    {
        AccountStatementJob job = AccountStatementJob.Create(8, Created);
        job.Claim("worker-1", Created, TimeSpan.FromMinutes(5));
        job.MarkCompleted("worker-1", Created.AddMinutes(1));

        Assert.Equal(AccountStatementJobStatuses.Completed, job.Status);
        Assert.Equal(1, job.AttemptCount);
        Assert.Null(job.LockedBy);
    }

    [Fact]
    public void AccountStatementJob_FinalFailureAndInvalidOperationsAreRejected()
    {
        AccountStatementJob job = AccountStatementJob.Create(8, Created);

        Assert.Throws<ArgumentException>(() => job.Claim("", Created, TimeSpan.FromMinutes(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => job.Claim("worker", Created, TimeSpan.Zero));
        Assert.Throws<InvalidOperationException>(() => job.MarkCompleted("worker", Created));

        job.Claim("worker", Created, TimeSpan.FromMinutes(1));
        job.MarkFailed("worker", "failure", Created.AddMinutes(1), TimeSpan.Zero, 1);

        Assert.Equal(AccountStatementJobStatuses.Failed, job.Status);
        Assert.Throws<InvalidOperationException>(() => job.MarkCompleted("worker", Created));
    }
}
