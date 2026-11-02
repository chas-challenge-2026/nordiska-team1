using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Nordiska.FrontendApi.Endpoints.Reporting;
using Nordiska.Modules.Reporting.Application;
using Nordiska.Modules.Reporting.Contracts.Requests;

namespace Nordiska.FrontendApi.IntegrationTests.Reporting;

public sealed class TaxReportControllerTests
{
    [Fact]
    public async Task Create_UsesAuthenticatedCustomerAndTaxYearOnly()
    {
        var service = new Mock<IAnnualTaxReportService>();
        service
            .Setup(s => s.RequestAsync(42, 2025, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RequestAnnualTaxReportResult(12, 7, "Pending", DateTimeOffset.UtcNow));

        var queries = new Mock<IAnnualTaxReportQueryService>();
        TaxReportController controller = CreateController(queries.Object, service: service.Object);

        ActionResult<AnnualTaxReportAcceptedResponse> response =
            await controller.Create(new AnnualTaxReportRequest(2025), CancellationToken.None);

        Assert.IsType<AcceptedResult>(response.Result);
        service.VerifyAll();
    }

    [Fact]
    public async Task GetStatus_CurrentCustomerOwnsReport_ReturnsStatus()
    {
        var status = new AnnualTaxReportJobStatus(
            JobId: 7,
            TaxReportId: 12,
            Status: "Completed",
            CreatedAt: DateTimeOffset.UtcNow,
            CompletedAt: DateTimeOffset.UtcNow,
            Error: null);

        var queries = new Mock<IAnnualTaxReportQueryService>();
        queries
            .Setup(query => query.GetStatusAsync(
                42,
                7,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(status);

        TaxReportController controller = CreateController(queries.Object);

        ActionResult<AnnualTaxReportJobStatus> response =
            await controller.GetStatus(7, CancellationToken.None);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Same(status, ok.Value);
    }

    [Fact]
    public async Task Download_CurrentCustomerOwnsReport_ReturnsPdf()
    {
        byte[] pdf = "%PDF-1.7 test"u8.ToArray();
        var document = new AnnualTaxReportDownload(
            "arsbesked-2025.pdf",
            "application/pdf",
            pdf);

        var queries = new Mock<IAnnualTaxReportQueryService>();
        queries
            .Setup(query => query.DownloadAsync(
                42,
                12,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        TaxReportController controller = CreateController(queries.Object);

        IActionResult response = await controller.Download(
            12,
            CancellationToken.None);

        FileContentResult file = Assert.IsType<FileContentResult>(response);
        Assert.Equal(pdf, file.FileContents);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal("arsbesked-2025.pdf", file.FileDownloadName);
    }

    [Fact]
    public async Task GetStatus_WhenCompleted_CallsSynchronizer()
    {
        var status = new AnnualTaxReportJobStatus(
            JobId: 7,
            TaxReportId: 12,
            Status: "Completed",
            CreatedAt: DateTimeOffset.UtcNow,
            CompletedAt: DateTimeOffset.UtcNow,
            Error: null);

        var queries = new Mock<IAnnualTaxReportQueryService>();
        queries
            .Setup(query => query.GetStatusAsync(
                42,
                7,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(status);

        var synchronizer = new Mock<IReportInboxSynchronizer>();

        TaxReportController controller = CreateController(queries.Object, synchronizer.Object);

        await controller.GetStatus(7, CancellationToken.None);

        synchronizer.Verify(
            s => s.SyncTaxReportAsync(42, 12, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static TaxReportController CreateController(
        IAnnualTaxReportQueryService queries,
        IReportInboxSynchronizer? synchronizer = null,
        IAnnualTaxReportService? service = null)
    {
        var controller = new TaxReportController(
            service ?? new Mock<IAnnualTaxReportService>().Object,
            queries,
            synchronizer);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(
                    new ClaimsIdentity(
                    [
                        new Claim(
                            ClaimTypes.NameIdentifier,
                            "42")
                    ],
                    "test"))
            }
        };

        return controller;
    }
}
