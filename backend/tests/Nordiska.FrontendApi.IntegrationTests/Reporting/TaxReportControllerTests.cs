using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Nordiska.FrontendApi.Endpoints.Reporting;
using Nordiska.Modules.Reporting.Application;

namespace Nordiska.FrontendApi.IntegrationTests.Reporting;

public sealed class TaxReportControllerTests
{
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

    private static TaxReportController CreateController(
        IAnnualTaxReportQueryService queries)
    {
        var service = new Mock<IAnnualTaxReportService>();

        var controller = new TaxReportController(
            service.Object,
            queries);

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
