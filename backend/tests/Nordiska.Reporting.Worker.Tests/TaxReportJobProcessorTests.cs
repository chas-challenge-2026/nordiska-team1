using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nordiska.Modules.Banking.Contracts.Responses;
using Nordiska.Modules.Reporting.Application;
using Nordiska.Modules.Reporting.Domain;
using Nordiska.Modules.Reporting.Infrastructure;

namespace Nordiska.Reporting.Worker.Tests;

public sealed class TaxReportJobProcessorTests
{
    [Fact]
    public async Task PdfReportGenerator_GeneratesNativePdf()
    {
        var generator = new PdfReportGenerator();
        var data = new TaxReportData(
            2,
            "NOR-123",
            "Savings",
            1,
            "Test Customer",
            "199001011234",
            2025,
            0,
            100,
            0.03m,
            3,
            0.9m,
            new[]
            {
                new TransactionResponse(1, 2, "deposit", 100m, DateTime.UtcNow)
            },
            DateTime.UtcNow);

        var pdf = await generator.GenerateTaxReportPdfAsync(data);

        Assert.NotEmpty(pdf);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(pdf, 0, 5));
    }

    [Fact]
    public async Task ProcessNextAsync_GeneratesStoresAndCompletesJob()
    {
        var job = CreateJob();
        var repository = new Mock<ITaxReportJobRepository>();
        var builder = new Mock<IReportDataBuilder>();
        var generator = new Mock<IPdfReportGenerator>();
        var storage = new Mock<IReportFileStorage>();

        repository
            .Setup(x => x.ClaimNextPendingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);
        builder
            .Setup(x => x.BuildAsync(1, 2, 2025, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateReportData());
        generator
            .Setup(x => x.GenerateTaxReportPdfAsync(It.IsAny<TaxReportData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        var processor = new TaxReportJobProcessor(
            repository.Object,
            builder.Object,
            generator.Object,
            storage.Object,
            NullLogger<TaxReportJobProcessor>.Instance);

        var processed = await processor.ProcessNextAsync(CancellationToken.None);

        Assert.True(processed);
        Assert.Equal("Done", job.Status);
        Assert.NotNull(job.CompletedAt);
        storage.Verify(x => x.SaveAsync(
            job.Id,
            It.Is<byte[]>(bytes => bytes.SequenceEqual(new byte[] { 0x25, 0x50, 0x44, 0x46 })),
            It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(x => x.UpdateAsync(job, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessNextAsync_WhenGenerationFails_MarksJobFailed()
    {
        var job = CreateJob();
        var repository = new Mock<ITaxReportJobRepository>();
        var builder = new Mock<IReportDataBuilder>();
        var generator = new Mock<IPdfReportGenerator>();
        var storage = new Mock<IReportFileStorage>();

        repository
            .Setup(x => x.ClaimNextPendingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);
        builder
            .Setup(x => x.BuildAsync(1, 2, 2025, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("test generation failure"));

        var processor = new TaxReportJobProcessor(
            repository.Object,
            builder.Object,
            generator.Object,
            storage.Object,
            NullLogger<TaxReportJobProcessor>.Instance);

        var processed = await processor.ProcessNextAsync(CancellationToken.None);

        Assert.True(processed);
        Assert.Equal("Failed", job.Status);
        Assert.Equal(5001, job.ErrorCode);
        Assert.Equal("test generation failure", job.ErrorMessage);
        repository.Verify(x => x.UpdateAsync(job, It.IsAny<CancellationToken>()), Times.Once);
        storage.VerifyNoOtherCalls();
    }

    private static TaxReportJob CreateJob() => new()
    {
        Id = Guid.NewGuid(),
        CustomerId = 1,
        AccountId = 2,
        Year = 2025,
        Status = "Pending",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static TaxReportData CreateReportData() => new(
        2,
        "NOR-123",
        "Savings",
        1,
        "Test Customer",
        "199001011234",
        2025,
        0,
        100,
        0.03m,
        3,
        0.9m,
        Array.Empty<TransactionResponse>(),
        DateTime.UtcNow);
}