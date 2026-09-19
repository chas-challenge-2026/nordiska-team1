using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nordiska.FrontendApi.IntegrationTests.Authentication;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Contracts.Responses;
using Xunit;

namespace Nordiska.FrontendApi.IntegrationTests.Banking;

public class PolicyRateIntegrationTests
{
    // Swaps the real Riksbank client for a fake so the tests never leave the machine
    private sealed class PolicyRateWebApplicationFactory : CustomAuthWebApplicationFactory
    {
        private readonly IRiksbankClient _riksbankClient;

        public PolicyRateWebApplicationFactory(IRiksbankClient riksbankClient)
        {
            _riksbankClient = riksbankClient;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IRiksbankClient>();
                services.AddSingleton(_riksbankClient);
            });
        }
    }

    private sealed class FakeRiksbankClient : IRiksbankClient
    {
        private readonly PolicyRateObservation? _observation;

        public FakeRiksbankClient(PolicyRateObservation? observation)
        {
            _observation = observation;
        }

        public Task<PolicyRateObservation> GetLatestPolicyRateAsync(CancellationToken cancellationToken = default)
        {
            if (_observation is null)
            {
                throw new HttpRequestException("Riksbank is down", null, HttpStatusCode.ServiceUnavailable);
            }

            return Task.FromResult(_observation);
        }
    }

    [Fact]
    public async Task GetPolicyRate_RiksbankUp_Returns200WithRateAsFraction()
    {
        using var factory = new PolicyRateWebApplicationFactory(new FakeRiksbankClient(new PolicyRateObservation(new DateOnly(2026, 9, 18), 1.75m)));
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/interest-rates/policy-rate");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var policyRate = await response.Content.ReadFromJsonAsync<PolicyRateResponse>();
        policyRate.Should().NotBeNull();
        policyRate!.Rate.Should().Be(0.0175m);
        policyRate.EffectiveDate.Should().Be(new DateOnly(2026, 9, 18));
        policyRate.Stale.Should().BeFalse();
    }

    [Fact]
    public async Task GetPolicyRate_RiksbankDownAndNothingCached_Returns503ProblemDetails()
    {
        using var factory = new PolicyRateWebApplicationFactory(new FakeRiksbankClient(null));
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/interest-rates/policy-rate");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Status.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }
}
