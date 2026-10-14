using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Nordiska.FrontendApi.Endpoints.Reporting;
using Nordiska.Modules.Reporting.Application;
using Xunit;

namespace Nordiska.FrontendApi.IntegrationTests.Reporting;

public sealed class AccountStatementControllerTests
{
    [Fact]
    public async Task GetStatus_WhenCompleted_CallsSynchronizer()
    {
        var status = new AccountStatementJobStatus(
            JobId: 10,
            AccountStatementId: 25,
            Status: "Completed",
            CreatedAt: DateTimeOffset.UtcNow,
            CompletedAt: DateTimeOffset.UtcNow,
            Error: null);

        var queries = new Mock<IAccountStatementQueryService>();
        queries
            .Setup(query => query.GetStatusAsync(
                42,
                10,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(status);

        var synchronizer = new Mock<IReportInboxSynchronizer>();

        AccountStatementController controller = CreateController(queries.Object, synchronizer.Object);

        await controller.GetStatus(10, CancellationToken.None);

        synchronizer.Verify(
            s => s.SyncAccountStatementAsync(42, 25, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static AccountStatementController CreateController(
        IAccountStatementQueryService queries,
        IReportInboxSynchronizer? synchronizer = null)
    {
        var service = new Mock<IAccountStatementService>();

        var controller = new AccountStatementController(
            service.Object,
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