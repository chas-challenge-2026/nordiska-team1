using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Nordiska.FrontendApi.Contracts.Requests;
using Nordiska.FrontendApi.IntegrationTests.Authentication;
using Nordiska.Modules.Banking.Contracts.Responses;
using Nordiska.Modules.Reporting.Contracts.Requests;
using Xunit;

namespace Nordiska.FrontendApi.IntegrationTests.Reporting;

public class ReportsControllerIntegrationTests : IClassFixture<CustomAuthWebApplicationFactory>
{
    private readonly CustomAuthWebApplicationFactory _factory;

    public ReportsControllerIntegrationTests(CustomAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string email = "anna@exempel.se")
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "password123"));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        return client;
    }

    [Fact]
    public async Task InitiateTaxReport_ValidAccount_ReturnsAcceptedWithJobId()
    {
        var client = await CreateAuthenticatedClientAsync();

        // Get an account for user
        var accountsResp = await client.GetAsync("/api/accounts");
        accountsResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var accounts = await accountsResp.Content.ReadFromJsonAsync<List<SavingsAccountResponse>>();
        accounts.Should().NotBeNull();
        accounts!.Count.Should().BeGreaterThan(0);
        var targetAccount = accounts[0];

        // 1. POST /api/reports/tax-report
        var postResp = await client.PostAsJsonAsync("/api/reports/tax-report", new TaxReportRequest(targetAccount.Id, 2026));
        postResp.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var job = await postResp.Content.ReadFromJsonAsync<TaxReportJobResponse>();
        job.Should().NotBeNull();
        job!.JobId.Should().StartWith("job_");
        job.Status.Should().BeOneOf("Processing", "Completed");

        // 2. GET /api/reports/jobs/{jobId}
        var statusResp = await client.GetAsync($"/api/reports/jobs/{job.JobId}");
        statusResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var statusJob = await statusResp.Content.ReadFromJsonAsync<TaxReportJobResponse>();
        statusJob.Should().NotBeNull();
        statusJob!.JobId.Should().Be(job.JobId);

        // 3. GET /api/reports/jobs/{jobId}/download
        var downloadResp = await client.GetAsync($"/api/reports/jobs/{job.JobId}/download");
        downloadResp.StatusCode.Should().Be(HttpStatusCode.OK);
        downloadResp.Content.Headers.ContentType?.MediaType.Should().Be("application/pdf");

        var pdfBytes = await downloadResp.Content.ReadAsByteArrayAsync();
        pdfBytes.Should().NotBeNull();
        pdfBytes.Length.Should().BeGreaterThan(50);

        // PDF signature check (%PDF-1.4 header)
        var pdfHeader = Encoding.ASCII.GetString(pdfBytes, 0, Math.Min(8, pdfBytes.Length));
        pdfHeader.Should().StartWith("%PDF");
    }

    [Fact]
    public async Task GetDirectTaxReport_ValidQuery_ReturnsPdfStreamDirectly()
    {
        var client = await CreateAuthenticatedClientAsync();

        var accountsResp = await client.GetAsync("/api/accounts");
        var accounts = await accountsResp.Content.ReadFromJsonAsync<List<SavingsAccountResponse>>();
        accounts.Should().NotBeNull();
        var targetAccount = accounts![0];

        // GET /api/reports/tax-report?accountId=...&year=2026
        var directResp = await client.GetAsync($"/api/reports/tax-report?accountId={targetAccount.Id}&year=2026");
        directResp.StatusCode.Should().Be(HttpStatusCode.OK);
        directResp.Content.Headers.ContentType?.MediaType.Should().Be("application/pdf");

        var pdfBytes = await directResp.Content.ReadAsByteArrayAsync();
        pdfBytes.Should().NotBeNull();
        var pdfHeader = Encoding.ASCII.GetString(pdfBytes, 0, Math.Min(8, pdfBytes.Length));
        pdfHeader.Should().StartWith("%PDF");
    }

    [Fact]
    public async Task ReportsEndpoints_Unauthenticated_ReturnsUnauthorized()
    {
        var unauthenticatedClient = _factory.CreateClient();

        var postResp = await unauthenticatedClient.PostAsJsonAsync("/api/reports/tax-report", new TaxReportRequest(1, 2026));
        postResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var getResp = await unauthenticatedClient.GetAsync("/api/reports/jobs/job_123");
        getResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var directResp = await unauthenticatedClient.GetAsync("/api/reports/tax-report?accountId=1&year=2026");
        directResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
