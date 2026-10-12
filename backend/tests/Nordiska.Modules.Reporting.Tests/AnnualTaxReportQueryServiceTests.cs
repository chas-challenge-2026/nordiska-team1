using Microsoft.EntityFrameworkCore;
using Nordiska.Modules.Reporting.Application;
using Nordiska.Modules.Reporting.Domain;
using Nordiska.Modules.Reporting.Infrastructure;
using Nordiska.Modules.Reporting.Infrastructure.Db;

namespace Nordiska.Modules.Reporting.Tests;

public sealed class AnnualTaxReportQueryServiceTests
{
    [Fact]
    public async Task GetStatusAsync_ReportBelongsToCustomer_ReturnsStatus()
    {
        await using ReportingDbContext dbContext = CreateDbContext();
        TaxReport report = CreateReport(customerId: 42);
        dbContext.TaxReports.Add(report);
        await dbContext.SaveChangesAsync();

        TaxReportJob job = TaxReportJob.Create(
            report.Id,
            DateTimeOffset.UtcNow);

        dbContext.TaxReportJobs.Add(job);
        await dbContext.SaveChangesAsync();

        var service = new AnnualTaxReportQueryService(
            dbContext,
            new StubDocumentStorage([]));

        AnnualTaxReportJobStatus? result =
            await service.GetStatusAsync(
                customerId: 42,
                job.Id,
                CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(job.Id, result.JobId);
        Assert.Equal(TaxReportJobStatuses.Pending, result.Status);
    }

    [Fact]
    public async Task GetStatusAsync_ReportBelongsToAnotherCustomer_ReturnsNull()
    {
        await using ReportingDbContext dbContext = CreateDbContext();
        TaxReport report = CreateReport(customerId: 42);
        dbContext.TaxReports.Add(report);
        await dbContext.SaveChangesAsync();

        TaxReportJob job = TaxReportJob.Create(
            report.Id,
            DateTimeOffset.UtcNow);

        dbContext.TaxReportJobs.Add(job);
        await dbContext.SaveChangesAsync();

        var service = new AnnualTaxReportQueryService(
            dbContext,
            new StubDocumentStorage([]));

        AnnualTaxReportJobStatus? result =
            await service.GetStatusAsync(
                customerId: 99,
                job.Id,
                CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task DownloadAsync_ReportBelongsToCustomer_ReturnsPdf()
    {
        await using ReportingDbContext dbContext = CreateDbContext();
        TaxReport report = CreateReport(customerId: 42);
        dbContext.TaxReports.Add(report);
        await dbContext.SaveChangesAsync();

        byte[] pdf = "%PDF-1.7 test"u8.ToArray();

        GeneratedDocument document = GeneratedDocument.Create(
            "AnnualTaxReport",
            "application/pdf",
            "arsbesked-2025.pdf",
            "tax-reports/1/document.pdf",
            pdf.LongLength,
            "hash",
            DateTimeOffset.UtcNow);

        dbContext.GeneratedDocuments.Add(document);
        await dbContext.SaveChangesAsync();

        dbContext.TaxReportDocuments.Add(
            TaxReportDocument.Create(
                report.Id,
                document.Id,
                DateTimeOffset.UtcNow));

        await dbContext.SaveChangesAsync();

        var service = new AnnualTaxReportQueryService(
            dbContext,
            new StubDocumentStorage(pdf));

        AnnualTaxReportDownload? result =
            await service.DownloadAsync(
                customerId: 42,
                report.Id,
                CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("application/pdf", result.ContentType);
        Assert.Equal("arsbesked-2025.pdf", result.FileName);
        Assert.Equal(pdf, result.Content);
    }

    private static ReportingDbContext CreateDbContext()
    {
        var options =
            new DbContextOptionsBuilder<ReportingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;

        return new ReportingDbContext(options);
    }

    private static TaxReport CreateReport(long customerId)
    {
        return TaxReport.Create(
            customerId,
            accountId: 10,
            taxYear: 2025,
            totalInterestMinor: 437500,
            taxDeductedMinor: 131250,
            currency: "SEK",
            customerName: "Anna Lindqvist",
            accountNumber: "NKM-10001",
            accountName: "Sparkonto",
            createdAt: DateTimeOffset.UtcNow,
            schemaVersion: "1.0",
            payloadHash: new string('0', 64));
    }

    private sealed class StubDocumentStorage(byte[] content)
        : IReportDocumentStorage
    {
        public Task SaveAsync(
            string storageKey,
            ReadOnlyMemory<byte> value,
            string expectedSha256Hash,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<byte[]> ReadAsync(
            string storageKey,
            string expectedSha256Hash,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(content);
        }
    }
}
